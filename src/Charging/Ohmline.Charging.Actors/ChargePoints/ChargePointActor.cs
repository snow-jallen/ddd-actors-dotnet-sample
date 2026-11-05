using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.ChargePoints;

/// <summary>
/// The shell around one charge point's decider. It owns what the decider must not: identity
/// (the persistence id), concurrency (one message at a time), durability (the journal), time
/// (the clock it reads once per command) and messaging (the reply). The rules stay in
/// <see cref="ChargePoint"/>; this class only moves data between the mailbox, the decider and
/// the journal.
/// </summary>
public sealed class ChargePointActor : ReceivePersistentActor
{
    private readonly ChargePointId _chargePointId;
    private readonly TimeProvider _clock;
    private ChargePointState _state = ChargePoint.Initial;

    public ChargePointActor(ChargePointId chargePointId, TimeProvider clock)
    {
        _chargePointId = chargePointId;
        _clock = clock;
        PersistenceId = PersistenceIdOf(chargePointId);

        Recover<ChargePointEvent>(Apply);

        // First match wins: questions are answered before anything is decided.
        Command<GetChargePointStatus>(_ =>
            Sender.Tell(_state.ToStatus(_chargePointId, LastSequenceNr)));
        Command<ChargePointCommand>(Handle);
    }

    /// <summary>Durable vocabulary: <c>charge-point-OHM-SLC-0042</c>, for ever.</summary>
    public override string PersistenceId { get; }

    public static string PersistenceIdOf(ChargePointId chargePointId) =>
        $"charge-point-{chargePointId}";

    public static Props Props(ChargePointId chargePointId, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new ChargePointActor(chargePointId, clock));

    private void Handle(ChargePointCommand command)
    {
        var replyTo = Sender;
        var decision = ChargePoint.Decide(_state, command, DecisionContext.From(_clock));
        switch (decision)
        {
            case Ok<ImmutableArray<ChargePointEvent>> { Value.IsEmpty: true }:
                replyTo.Tell(new CommandAccepted(LastSequenceNr));
                break;
            case Ok<ImmutableArray<ChargePointEvent>> accepted:
                // One decision, one atomic write. Each event is applied once it is stored, and
                // the sender hears yes only after the last one.
                PersistAll(
                    accepted.Value,
                    Apply,
                    () => replyTo.Tell(new CommandAccepted(LastSequenceNr)));
                break;
            case Err<ChargePointError> rejected:
                replyTo.Tell(rejected.Error.ToReply());
                break;
            case null:
                throw new InvalidOperationException("Decide returned a default Result.");
        }
    }

    private void Apply(ChargePointEvent @event) => _state = ChargePoint.Evolve(_state, @event);
}
