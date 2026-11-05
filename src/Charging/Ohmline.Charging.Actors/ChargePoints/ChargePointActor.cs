using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// One charge point's decider in the shell. What is left here is what is particular to charge
/// points: the durable name, the replies in Charging's vocabulary, and the status query. The
/// rules stay in <see cref="ChargePoint"/>; the command path is <see cref="DeciderActor{TDecider,
/// TState, TCommand, TEvent, TError}"/>'s.
/// </summary>
public sealed class ChargePointActor
    : DeciderActor<ChargePoint, ChargePointState, ChargePointCommand, ChargePointEvent,
        ChargePointError>
{
    /// <summary>Durable vocabulary: the shard type name and the persistence id prefix.</summary>
    public const string TypeName = "charge-point";

    public ChargePointActor(ChargePointId chargePointId, TimeProvider clock)
        : base(TypeName, chargePointId.Value, clock) =>
        Command<GetChargePointStatus>(_ =>
            Sender.Tell(State.ToStatus(chargePointId, LastSequenceNr)));

    /// <summary>Durable vocabulary: <c>charge-point-OHM-SLC-0042</c>, for ever.</summary>
    public static string PersistenceIdOf(ChargePointId chargePointId) =>
        $"{TypeName}-{chargePointId}";

    public static Props Props(ChargePointId chargePointId, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new ChargePointActor(chargePointId, clock));

    protected override object Accepted(long version) => new CommandAccepted(version);

    protected override object Rejected(ChargePointError error) => error.ToReply();

    protected override object? ToSnapshot(ChargePointState state) => state.ToSnapshot();

    protected override ChargePointState FromSnapshot(object snapshot) =>
        ((ChargePointSnapshot)snapshot).ToState();
}
