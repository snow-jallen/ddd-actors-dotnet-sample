using System.Collections.Immutable;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>
/// The charge point aggregate as a decider: pure functions from what it remembers and what it is
/// asked to what happened. No clock, no I/O and no actor: the time arrives in the context. A
/// class only so that it can be a type argument; it is never instantiated.
/// </summary>
public sealed class ChargePoint
    : IDecider<ChargePointState, ChargePointCommand, ChargePointEvent, ChargePointError>
{
    private ChargePoint()
    {
    }

    public static ChargePointState Initial => ChargePointState.Initial;

    /// <summary>How long a reservation holds a connector for its driver.</summary>
    public static TimeSpan ReservationHold { get; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Decides, then stamps every event of the decision with the command that caused it and
    /// the context's time. The table says what happened; the stamp says why and when.
    /// </summary>
    public static Result<ImmutableArray<ChargePointEvent>, ChargePointError> Decide(
        ChargePointState state, ChargePointCommand command, DecisionContext context) =>
        DecisionTable(state, command, context).Map(events => Stamp(events, command, context));

    /// <summary>
    /// The charge point's rules as a decision table: one row per (state, command) pair. The
    /// first row is the factory: the only way a charge point comes into being.
    /// </summary>
    private static Result<ImmutableArray<ChargePointEvent>, ChargePointError> DecisionTable(
        ChargePointState state, ChargePointCommand command, DecisionContext context) =>
        (state, command) switch
        {
            (Unregistered, RegisterChargePoint register) => Accept(new ChargePointRegistered(
                register.ChargePointId, register.SiteId, register.ConnectorMaxPowers)),
            (Unregistered, ConnectorCommand) =>
                Reject(new ChargePointError.NotRegistered(command.ChargePointId)),
            (Registered, RegisterChargePoint) =>
                Reject(new ChargePointError.AlreadyRegistered(command.ChargePointId)),
            (Registered chargePoint, ConnectorCommand onConnector) =>
                chargePoint.Connectors.TryGetValue(onConnector.ConnectorNumber, out var connector)
                    ? Decide(connector.State, onConnector, context)
                    : Reject(new ChargePointError.UnknownConnector(onConnector.ConnectorNumber)),
            (null, _) => throw DefaultState(),
        };

    private static Result<ImmutableArray<ChargePointEvent>, ChargePointError> Decide(
        ConnectorState state, ConnectorCommand command, DecisionContext context) =>
        (state, command) switch
        {
            (Available, ReserveConnector reserve) => Accept(Reservation(reserve, context)),
            (Reserved held, ReserveConnector reserve) when held.HasExpiredAt(context.Now) =>
                Accept(Expiry(reserve, held), Reservation(reserve, context)),
            (Reserved held, ReserveConnector reserve) when held.TokenUid == reserve.TokenUid =>
                NoChange,
            (Reserved held, ReserveConnector) => Reject(HeldForAnother(command, held)),
            (Occupied or Unavailable, ReserveConnector) => Reject(Unusable(command)),
            (Faulted fault, ReserveConnector) => Reject(Broken(command, fault)),

            (Reserved held, CancelReservation cancel) when held.TokenUid == cancel.TokenUid =>
                Accept(new ReservationCancelled(
                    cancel.ChargePointId, cancel.ConnectorNumber, cancel.TokenUid)),
            (Reserved held, CancelReservation) => Reject(HeldForAnother(command, held)),
            (Available or Occupied or Faulted or Unavailable, CancelReservation) => NoChange,

            (Reserved held, ExpireReservation expire) when held.HasExpiredAt(context.Now) =>
                Accept(Expiry(expire, held)),
            (Available or Reserved or Occupied or Faulted or Unavailable, ExpireReservation) =>
                NoChange,

            (Available, OccupyConnector occupy) => Accept(Occupation(occupy)),
            (Reserved held, OccupyConnector occupy) when held.TokenUid == occupy.TokenUid =>
                Accept(Occupation(occupy)),
            (Reserved held, OccupyConnector occupy) when held.HasExpiredAt(context.Now) =>
                Accept(Expiry(occupy, held), Occupation(occupy)),
            (Reserved held, OccupyConnector) => Reject(HeldForAnother(command, held)),
            (Occupied current, OccupyConnector occupy) when current.SessionId == occupy.SessionId =>
                NoChange,
            (Occupied or Unavailable, OccupyConnector) => Reject(Unusable(command)),
            (Faulted fault, OccupyConnector) => Reject(Broken(command, fault)),

            (Occupied current, ReleaseConnector release) => Accept(new ConnectorReleased(
                release.ChargePointId, release.ConnectorNumber, current.SessionId)),
            (Available or Reserved or Faulted or Unavailable, ReleaseConnector) => NoChange,

            (Available or Reserved or Occupied or Unavailable, ReportConnectorFault report) =>
                Accept(new ConnectorFaulted(
                    report.ChargePointId, report.ConnectorNumber, report.ErrorCode)),
            (Faulted, ReportConnectorFault) => NoChange,

            (Faulted, ClearConnectorFault clear) => Accept(
                new ConnectorFaultCleared(clear.ChargePointId, clear.ConnectorNumber)),
            (Available or Reserved or Occupied or Unavailable, ClearConnectorFault) => NoChange,

            (Available or Reserved, MakeConnectorUnavailable close) => Accept(
                new ConnectorMadeUnavailable(close.ChargePointId, close.ConnectorNumber)),
            (Unavailable, MakeConnectorUnavailable) => NoChange,
            (Occupied, MakeConnectorUnavailable) => Reject(Unusable(command)),
            (Faulted fault, MakeConnectorUnavailable) => Reject(Broken(command, fault)),

            (Unavailable, MakeConnectorAvailable open) => Accept(
                new ConnectorMadeAvailable(open.ChargePointId, open.ConnectorNumber)),
            (Available or Reserved or Occupied, MakeConnectorAvailable) => NoChange,
            (Faulted fault, MakeConnectorAvailable) => Reject(Broken(command, fault)),

            (null, _) => throw DefaultState(),
        };

    /// <summary>
    /// Applies one event to the state. Total: every event meets some state and gives a state,
    /// and nothing is checked again. The event is a fact that Decide already accepted, and
    /// replaying the journal must never fail.
    /// </summary>
    public static ChargePointState Evolve(ChargePointState state, ChargePointEvent @event) =>
        (state, @event) switch
        {
            (Unregistered, ChargePointRegistered registered) => Register(registered),
            (Registered chargePoint, ConnectorEvent happened) =>
                chargePoint.Connectors.TryGetValue(happened.ConnectorNumber, out var connector)
                    ? chargePoint with
                    {
                        Connectors = chargePoint.Connectors.SetItem(
                            happened.ConnectorNumber, connector with { State = Next(happened) }),
                    }
                    : chargePoint,
            (Registered, ChargePointRegistered) or (Unregistered, ConnectorEvent) => state,
            (null, _) => throw DefaultState(),
        };

    /// <summary>
    /// Recovery: the state is the fold of the history over <see cref="Evolve"/>, starting from
    /// the initial state. The same history always gives the same state.
    /// </summary>
    public static ChargePointState Fold(IEnumerable<ChargePointEvent> events) =>
        Decider.Fold<ChargePoint, ChargePointState, ChargePointCommand, ChargePointEvent,
            ChargePointError>(events);

    private static ImmutableArray<ChargePointEvent> Stamp(
        ImmutableArray<ChargePointEvent> events,
        ChargePointCommand command,
        DecisionContext context) =>
        [.. events.Select(@event =>
            @event with { CommandId = command.CommandId, OccurredAt = context.Now })];

    private static Registered Register(ChargePointRegistered registered) =>
        new(registered.ChargePointId, registered.SiteId, registered.ConnectorMaxPowers
            .Index()
            .ToImmutableDictionary(
                entry => entry.Index + 1,
                entry => new Connector(
                    new ConnectorId(registered.ChargePointId, entry.Index + 1),
                    entry.Item,
                    new Available())));

    /// <summary>A connector's next state follows from the event alone.</summary>
    private static ConnectorState Next(ConnectorEvent happened) => happened switch
    {
        ConnectorReserved reserved => new Reserved(reserved.TokenUid, reserved.ExpiresAt),
        ConnectorOccupied occupied => new Occupied(occupied.SessionId),
        ConnectorFaulted faulted => new Faulted(faulted.ErrorCode),
        ConnectorMadeUnavailable => new Unavailable(),
        ReservationCancelled or ReservationExpired or ConnectorReleased
            or ConnectorFaultCleared or ConnectorMadeAvailable => new Available(),
    };

    /// <summary>The expiry is decided here, from the context's time, and recorded.</summary>
    private static ConnectorReserved Reservation(
        ReserveConnector reserve, DecisionContext context) =>
        new(reserve.ChargePointId, reserve.ConnectorNumber, reserve.TokenUid,
            context.Now + ReservationHold);

    private static ReservationExpired Expiry(ConnectorCommand command, Reserved held) =>
        new(command.ChargePointId, command.ConnectorNumber, held.TokenUid);

    private static ConnectorOccupied Occupation(OccupyConnector occupy) =>
        new(occupy.ChargePointId, occupy.ConnectorNumber, occupy.SessionId, occupy.TokenUid);

    private static ChargePointError HeldForAnother(ConnectorCommand command, Reserved held) =>
        new ChargePointError.ReservedForAnotherToken(command.ConnectorNumber, held.ExpiresAt);

    private static ChargePointError Unusable(ConnectorCommand command) =>
        new ChargePointError.ConnectorUnavailable(command.ConnectorNumber);

    private static ChargePointError Broken(ConnectorCommand command, Faulted fault) =>
        new ChargePointError.ConnectorFaulted(command.ConnectorNumber, fault.ErrorCode);

    private static Result<ImmutableArray<ChargePointEvent>, ChargePointError> NoChange { get; } =
        Accept();

    private static Result<ImmutableArray<ChargePointEvent>, ChargePointError> Accept(
        params ImmutableArray<ChargePointEvent> events) => Result.Ok(events);

    private static Result<ImmutableArray<ChargePointEvent>, ChargePointError> Reject(
        ChargePointError error) => Result.Fail(error);

    private static InvalidOperationException DefaultState() =>
        new("The state is default: it holds no case. Start from ChargePointState.Initial.");
}
