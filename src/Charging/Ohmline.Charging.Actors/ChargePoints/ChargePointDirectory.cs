using Akka.Actor;
using Akka.Pattern;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// Finds the actor for a charge point, starting it on the first message, and forwards the
/// message to it with its sender. A stand-in for one node until Chapter 12 replaces it with a
/// shard region, which does the same across a cluster and guarantees one actor per charge point
/// in all of it. Each charge point runs under a backoff supervisor, which starts it again after
/// a journal failure has stopped it.
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
            chargePoint = Context.ActorOf(Supervised(chargePointId), name);
        }

        chargePoint.Forward(message);
    }

    /// <summary>
    /// A charge point stops when the journal fails a write or a recovery (section 11.6). Its
    /// supervisor starts it again after one second, then two, four, and so on up to 30 s, with
    /// some jitter, so that a database that is down is not hammered by thousands of restarts at
    /// once; it never gives up. Messages that arrive while it is stopped are dead letters: their
    /// senders time out and retry. A journal failure stops a persistent actor rather than
    /// throwing, which is why this is OnStop and not OnFailure.
    /// </summary>
    private Props Supervised(ChargePointId chargePointId) =>
        BackoffSupervisor.Props(
            Backoff.OnStop(
                    ChargePointActor.Props(chargePointId, _clock),
                    childName: "entity",
                    minBackoff: TimeSpan.FromSeconds(1),
                    maxBackoff: TimeSpan.FromSeconds(30),
                    randomFactor: 0.2,
                    maxNrOfRetries: -1)
                .WithAutoReset(TimeSpan.FromSeconds(10)));
}
