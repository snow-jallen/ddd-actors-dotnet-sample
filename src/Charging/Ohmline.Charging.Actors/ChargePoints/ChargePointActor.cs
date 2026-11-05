using System.Collections.Immutable;
using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// One charge point's decider in the shell. What is left here is what is particular to charge
/// points: the durable name, the replies in Charging's vocabulary, the status query, and the
/// timers that expire reservations. The rules stay in <see cref="ChargePoint"/>; the command
/// path is <see cref="DeciderActor{TDecider, TState, TCommand, TEvent, TError}"/>'s.
/// </summary>
public sealed class ChargePointActor
    : DeciderActor<ChargePoint, ChargePointState, ChargePointCommand, ChargePointEvent,
        ChargePointError>,
        IWithTimers
{
    /// <summary>Durable vocabulary: the shard type name and the persistence id prefix.</summary>
    public const string TypeName = "charge-point";

    private readonly TimeProvider _clock;

    public ChargePointActor(ChargePointId chargePointId, TimeProvider clock)
        : base(TypeName, chargePointId.Value, clock)
    {
        _clock = clock;
        Command<GetChargePointStatus>(_ =>
            Sender.Tell(State.ToStatus(chargePointId, LastSequenceNr)));

        // Time asks, the decider decides: a lapsed timer is only a command like any other.
        Command<ReservationTimeUp>(timeUp => Handle(
            new ExpireReservation(CommandId.New(clock), chargePointId, timeUp.ConnectorNumber),
            ActorRefs.Nobody));
    }

    /// <summary>Set by Akka.NET before the actor starts; cancelled when it stops.</summary>
    public ITimerScheduler Timers { get; set; } = null!;

    /// <summary>Durable vocabulary: <c>charge-point-OHM-SLC-0042</c>, for ever.</summary>
    public static string PersistenceIdOf(ChargePointId chargePointId) =>
        $"{TypeName}-{chargePointId}";

    /// <summary>
    /// For a test that starts the actor itself, with a clock of its choosing. In a node, the
    /// shard region has the container make it (section 12.3).
    /// </summary>
    public static Props Props(ChargePointId chargePointId, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new ChargePointActor(chargePointId, clock));

    protected override object Accepted(long version) => new CommandAccepted(version);

    protected override object Rejected(ChargePointError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new CommandRejected(code, message);

    protected override CommandId CommandIdOf(ChargePointCommand command) => command.CommandId;

    protected override CommandId CausationOf(ChargePointEvent @event) => @event.CommandId;

    protected override object? ToSnapshot(
        ChargePointState state, ImmutableArray<RecentCommand> recent) =>
        state.ToSnapshot() is { } snapshot ? snapshot with { RecentCommands = recent } : null;

    protected override (ChargePointState State, ImmutableArray<RecentCommand> Recent) FromSnapshot(
        object snapshot)
    {
        var stored = (ChargePointSnapshot)snapshot;
        return (stored.ToState(), stored.RecentCommands);
    }

    /// <summary>Whatever happened to a connector, its timer follows its new state.</summary>
    protected override void OnEventApplied(ChargePointEvent @event)
    {
        if (@event is ConnectorEvent happened)
        {
            FollowReservation(happened.ConnectorNumber);
        }
    }

    /// <summary>
    /// A recovered actor has no timers: they lived in the incarnation that stopped. The state
    /// says which reservations are still running, so their timers start again from it.
    /// </summary>
    protected override void OnRecovered()
    {
        if (State.Value is Registered registered)
        {
            foreach (var connectorNumber in registered.Connectors.Keys)
            {
                FollowReservation(connectorNumber);
            }
        }
    }

    /// <summary>
    /// One timer per reserved connector, keyed by it, due when the reservation expires (at once
    /// if it already has); none for any other connector. Starting a timer with a key replaces
    /// the one before it.
    /// </summary>
    private void FollowReservation(int connectorNumber)
    {
        var key = $"reservation-{connectorNumber}";
        if (State.Value is Registered registered
            && registered.Connectors.TryGetValue(connectorNumber, out var connector)
            && connector.State.Value is Reserved held)
        {
            var due = held.ExpiresAt - _clock.GetUtcNow();
            Timers.StartSingleTimer(
                key, new ReservationTimeUp(connectorNumber), due > TimeSpan.Zero ? due : default);
        }
        else
        {
            Timers.Cancel(key);
        }
    }

    /// <summary>A note to itself. It never leaves the actor, so it is never serialized.</summary>
    private sealed record ReservationTimeUp(int ConnectorNumber)
        : INoSerializationVerificationNeeded;
}
