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
            (Active or SuspendedByEv or SuspendedByCharger or Ended, StartSession) =>
                Reject(new SessionError.AlreadyStarted(command.SessionId)),

            (Active active, RecordMeterValue value) => Record(active.Terms, active.Meter, value),
            (SuspendedByEv idle, RecordMeterValue value) => Record(idle.Terms, idle.Meter, value),
            (SuspendedByCharger paused, RecordMeterValue value) =>
                Record(paused.Terms, paused.Meter, value),
            (Ended, RecordMeterValue) => NoChange,

            (Active or SuspendedByCharger, SuspendCharging { By: ByEv } suspend) =>
                Accept(new ChargingSuspendedByEv(suspend.SessionId)),
            (SuspendedByEv, SuspendCharging { By: ByEv }) => NoChange,
            (Active or SuspendedByEv, SuspendCharging { By: ByCharger } suspend) =>
                Accept(new ChargingSuspendedByCharger(suspend.SessionId)),
            (SuspendedByCharger, SuspendCharging { By: ByCharger }) => NoChange,
            (SuspendedByEv or SuspendedByCharger, ResumeCharging resume) =>
                Accept(new ChargingResumed(resume.SessionId)),
            (Active, ResumeCharging) => NoChange,
            (Ended, SuspendCharging or ResumeCharging) =>
                Reject(new SessionError.AlreadyEnded(command.SessionId)),

            (Active active, StopSession) => Accept(End(active.Terms, active.Meter)),
            (SuspendedByEv idle, StopSession) => Accept(End(idle.Terms, idle.Meter)),
            (SuspendedByCharger paused, StopSession) => Accept(End(paused.Terms, paused.Meter)),
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
            (Active active, MeterValueRecorded { Phase: null } recorded) =>
                active with { Meter = active.Meter.Advance(Reading(recorded)) },
            (SuspendedByEv idle, MeterValueRecorded { Phase: null } recorded) =>
                idle with { Meter = idle.Meter.Advance(Reading(recorded)) },
            (SuspendedByCharger paused, MeterValueRecorded { Phase: null } recorded) =>
                paused with { Meter = paused.Meter.Advance(Reading(recorded)) },

            (Active active, ChargingSuspendedByEv suspended) =>
                new SuspendedByEv(active.Terms, active.Meter, suspended.OccurredAt),
            (SuspendedByCharger paused, ChargingSuspendedByEv suspended) =>
                new SuspendedByEv(paused.Terms, paused.Meter, suspended.OccurredAt),
            (Active active, ChargingSuspendedByCharger suspended) =>
                new SuspendedByCharger(active.Terms, active.Meter, suspended.OccurredAt),
            (SuspendedByEv idle, ChargingSuspendedByCharger suspended) =>
                new SuspendedByCharger(idle.Terms, idle.Meter, suspended.OccurredAt),
            (SuspendedByEv idle, ChargingResumed) => new Active(idle.Terms, idle.Meter),
            (SuspendedByCharger paused, ChargingResumed) => new Active(paused.Terms, paused.Meter),

            (Active active, SessionEnded ended) =>
                new Ended(active.Terms, ended.EnergyDelivered, ended.OccurredAt),
            (SuspendedByEv idle, SessionEnded ended) =>
                new Ended(idle.Terms, ended.EnergyDelivered, ended.OccurredAt),
            (SuspendedByCharger paused, SessionEnded ended) =>
                new Ended(paused.Terms, ended.EnergyDelivered, ended.OccurredAt),

            // Anomalies, the spend cap and a single phase's register are facts to remember in
            // the journal, not in memory, and an event that meets a state Decide would not have
            // produced it in changes nothing. Evolve never refuses.
            (NoSession or Active or SuspendedByEv or SuspendedByCharger or Ended, SessionEvent) =>
                state,
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
            _ when start.SpendCap.Currency != start.Tariff.Currency =>
                Reject(new CurrencyMismatch(start.Tariff.Currency, start.SpendCap.Currency)),
            _ => Accept(new SessionStarted(
                start.SessionId,
                start.SiteId,
                start.ConnectorId,
                start.TokenUid,
                start.Tariff,
                start.SpendCap,
                start.MeterStartWh,
                start.PhaseCount)),
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

    /// <summary>The charger counts Wh; the session records energy, over every phase.</summary>
    private static MeterValueRecorded Recorded(RecordMeterValue value) =>
        new(value.SessionId, Energy.FromWattHours(value.RegisterWh), value.MeasuredAt);

    private static MeterReading Reading(MeterValueRecorded recorded) =>
        new(recorded.Register, recorded.MeasuredAt);

    /// <summary>
    /// The terms the session started with. A session stored before the phase count was
    /// recorded has none, and gets the assumed one: Evolve fills the gap, Decide never sees it.
    /// </summary>
    private static SessionTerms Terms(SessionStarted started) =>
        new(started.SessionId, started.SiteId, started.ConnectorId, started.TokenUid,
            started.Tariff, started.SpendCap)
        {
            PhaseCount = started.PhaseCount ?? SessionTerms.AssumedPhaseCount,
        };

    private static Result<ImmutableArray<SessionEvent>, SessionError> NoChange { get; } =
        Accept();

    private static Result<ImmutableArray<SessionEvent>, SessionError> Accept(
        params ImmutableArray<SessionEvent> events) => Result.Ok(events);

    private static Result<ImmutableArray<SessionEvent>, SessionError> Reject(
        SessionError error) => Result.Fail(error);

    private static InvalidOperationException DefaultState() =>
        new("The state is default: it holds no case. Start from SessionState.Initial.");
}
