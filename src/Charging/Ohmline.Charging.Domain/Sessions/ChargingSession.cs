using System.Collections.Immutable;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Metering;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// The charging session as a decider. Most of what it hears are facts the charge point reports,
/// and a fact cannot be refused: a meter reading that breaks the rules is recorded as an anomaly,
/// reaching the spend cap is recorded, and a repeated stop changes nothing.
/// </summary>
public sealed class ChargingSession
    : IDecider<SessionState, SessionCommand, SessionEvent, SessionError>
{
    private ChargingSession()
    {
    }

    public static SessionState Initial => SessionState.Initial;

    /// <summary>
    /// Decides, then stamps every event of the decision with the command that caused it and
    /// the context's time.
    /// </summary>
    public static Result<ImmutableArray<SessionEvent>, SessionError> Decide(
        SessionState state, SessionCommand command, DecisionContext context) =>
        DecisionTable(state, command, context).Map(events => Stamp(events, command, context));

    private static Result<ImmutableArray<SessionEvent>, SessionError> DecisionTable(
        SessionState state, SessionCommand command, DecisionContext context) =>
        (state, command) switch
        {
            (NoSession, StartSession start) => Start(start),
            (NoSession, RecordMeterValue or SuspendCharging or ResumeCharging or StopSession) =>
                Reject(new SessionError.NotStarted(command.SessionId)),
            (Active or Suspended or Ended, StartSession) =>
                Reject(new SessionError.AlreadyStarted(command.SessionId)),

            (Active active, RecordMeterValue value) => Record(active.Terms, active.Meter, value),
            (Suspended paused, RecordMeterValue value) => Record(paused.Terms, paused.Meter, value),
            (Ended, RecordMeterValue) => NoChange,

            (Active, SuspendCharging suspend) => Accept(new ChargingSuspended(suspend.SessionId)),
            (Suspended, SuspendCharging) => NoChange,
            (Suspended, ResumeCharging resume) => Accept(new ChargingResumed(resume.SessionId)),
            (Active, ResumeCharging) => NoChange,
            (Ended, SuspendCharging or ResumeCharging) =>
                Reject(new SessionError.AlreadyEnded(command.SessionId)),

            (Active active, StopSession) => Accept(End(active.Terms, active.Meter)),
            (Suspended paused, StopSession) => Accept(End(paused.Terms, paused.Meter)),
            (Ended, StopSession) => NoChange,

            (null, _) => throw DefaultState(),
        };

    public static SessionState Evolve(SessionState state, SessionEvent @event) =>
        (state, @event) switch
        {
            (NoSession, SessionStarted started) => new Active(
                Terms(started),
                SessionMeter.StartingAt(
                    MeterReading.FromWattHours(started.MeterStartWh, started.OccurredAt))),
            (Active active, MeterValueRecorded recorded) =>
                active with { Meter = active.Meter.Advance(Reading(recorded)) },
            (Suspended paused, MeterValueRecorded recorded) =>
                paused with { Meter = paused.Meter.Advance(Reading(recorded)) },
            (Active active, ChargingSuspended suspended) =>
                new Suspended(active.Terms, active.Meter, suspended.OccurredAt),
            (Suspended paused, ChargingResumed) => new Active(paused.Terms, paused.Meter),
            (Active active, SessionEnded ended) =>
                new Ended(active.Terms, ended.EnergyDelivered, ended.OccurredAt),
            (Suspended paused, SessionEnded ended) =>
                new Ended(paused.Terms, ended.EnergyDelivered, ended.OccurredAt),

            // Anomalies and the spend cap are facts to remember in the journal, not in memory,
            // and an event that meets a state Decide would not have produced it in changes
            // nothing. Evolve never refuses.
            (NoSession or Active or Suspended or Ended, SessionEvent) => state,
            (null, _) => throw DefaultState(),
        };

    public static SessionState Fold(IEnumerable<SessionEvent> events) =>
        Decider.Fold<ChargingSession, SessionState, SessionCommand, SessionEvent, SessionError>(
            events);

    /// <summary>The factory: the terms are checked once, here, and locked in the event.</summary>
    private static Result<ImmutableArray<SessionEvent>, SessionError> Start(StartSession start) =>
        start switch
        {
            { SpendCap.Amount: <= 0m } =>
                Reject(new SessionError.SpendCapNotPositive(start.SpendCap)),
            _ when start.SpendCap.Currency != start.PricePerKwh.Currency =>
                Reject(new CurrencyMismatch(start.PricePerKwh.Currency, start.SpendCap.Currency)),
            _ => Accept(new SessionStarted(
                start.SessionId,
                start.SiteId,
                start.ConnectorId,
                start.TokenUid,
                start.TariffId,
                start.TariffVersion,
                start.PricePerKwh,
                start.SpendCap,
                start.MeterStartWh)),
        };

    private static Result<ImmutableArray<SessionEvent>, SessionError> Record(
        SessionTerms terms, SessionMeter meter, RecordMeterValue value)
    {
        var reading = MeterReading.FromWattHours(value.RegisterWh, value.MeasuredAt);
        if (reading == meter.Latest)
        {
            return NoChange;
        }

        return reading.EnergySince(meter.Latest) switch
        {
            Ok<Energy> => SpendCapCrossed(terms, meter, meter.Advance(reading)) is { } reached
                ? Accept(Recorded(value), reached)
                : Accept(Recorded(value)),
            Err<MeterError> => Accept(new MeterAnomalyDetected(
                value.SessionId,
                value.RegisterWh,
                value.MeasuredAt,
                (long)meter.Latest.Register.WattHours,
                meter.Latest.At)),
            null => throw new InvalidOperationException("EnergySince returned a default Result."),
        };
    }

    /// <summary>The cap is reached by the reading that crosses it, and by no later one.</summary>
    private static SpendCapReached? SpendCapCrossed(
        SessionTerms terms, SessionMeter before, SessionMeter after)
    {
        var cost = terms.CostOf(after.Delivered);
        return terms.CostOf(before.Delivered) < terms.SpendCap && cost >= terms.SpendCap
            ? new SpendCapReached(terms.SessionId, cost, terms.SpendCap)
            : null;
    }

    private static SessionEnded End(SessionTerms terms, SessionMeter meter) =>
        new(terms.SessionId, terms.SiteId, terms.ConnectorId, terms.TokenUid, meter.Delivered);

    private static ImmutableArray<SessionEvent> Stamp(
        ImmutableArray<SessionEvent> events, SessionCommand command, DecisionContext context) =>
        [.. events.Select(@event =>
            @event with { CommandId = command.CommandId, OccurredAt = context.Now })];

    private static MeterValueRecorded Recorded(RecordMeterValue value) =>
        new(value.SessionId, value.RegisterWh, value.MeasuredAt);

    private static MeterReading Reading(MeterValueRecorded recorded) =>
        MeterReading.FromWattHours(recorded.RegisterWh, recorded.MeasuredAt);

    private static SessionTerms Terms(SessionStarted started) =>
        new(started.SessionId, started.SiteId, started.ConnectorId, started.TokenUid,
            started.TariffId, started.TariffVersion, started.PricePerKwh, started.SpendCap);

    private static Result<ImmutableArray<SessionEvent>, SessionError> NoChange { get; } =
        Accept();

    private static Result<ImmutableArray<SessionEvent>, SessionError> Accept(
        params ImmutableArray<SessionEvent> events) => Result.Ok(events);

    private static Result<ImmutableArray<SessionEvent>, SessionError> Reject(
        SessionError error) => Result.Fail(error);

    private static InvalidOperationException DefaultState() =>
        new("The state is default: it holds no case. Start from SessionState.Initial.");
}
