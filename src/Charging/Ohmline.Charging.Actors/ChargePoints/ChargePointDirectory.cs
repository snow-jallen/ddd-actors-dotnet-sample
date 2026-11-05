using Akka.Actor;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// Finds the actor for a charge point, starting it on the first message, and forwards the
/// message to it with its sender. A stand-in for one node until Chapter 12 replaces it with a
/// shard region, which does the same across a cluster and guarantees one actor per charge point
/// in all of it.
/// </summary>
public sealed class ChargePointDirectory : ReceiveActor
{
    private readonly TimeProvider _clock;

    public ChargePointDirectory(TimeProvider clock)
    {
        _clock = clock;
        Receive<ChargePointCommand>(command => ForwardTo(command.ChargePointId, command));
        Receive<ChargePointQuery>(query => ForwardTo(query.ChargePointId, query));
    }

    public static Props Props(TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new ChargePointDirectory(clock));

    private void ForwardTo(ChargePointId chargePointId, object message)
    {
        var name = chargePointId.Value;
        var chargePoint = Context.Child(name);
        if (chargePoint.IsNobody())
        {
            chargePoint = Context.ActorOf(ChargePointActor.Props(chargePointId, _clock), name);
        }

        chargePoint.Forward(message);
    }
}
