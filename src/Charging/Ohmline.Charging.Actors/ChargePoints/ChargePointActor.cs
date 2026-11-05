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
/// WRONG ON PURPOSE (branch wrong/ch10-s10-logic-in-receive). Reservations are decided here, in
/// a Receive handler, instead of by <see cref="ChargePoint.Decide"/>. The rule written here looks
/// right and forgets that a reservation expires, and no decider test sees it, because the
/// decider is no longer asked.
/// </summary>
public sealed class ChargePointActor : ReceivePersistentActor
{
    public const string TypeName = "charge-point";

    private readonly TimeProvider _clock;
    private ChargePointState _state = ChargePoint.Initial;

    public ChargePointActor(ChargePointId chargePointId, TimeProvider clock)
    {
        _clock = clock;
        PersistenceId = PersistenceIdOf(chargePointId);

        Recover<ChargePointEvent>(@event => _state = ChargePoint.Evolve(_state, @event));

        Command<GetChargePointStatus>(_ =>
            Sender.Tell(_state.ToStatus(chargePointId, LastSequenceNr)));
        Command<ReserveConnector>(Reserve);
        Command<ChargePointCommand>(Decide);
    }

    public override string PersistenceId { get; }

    public static string PersistenceIdOf(ChargePointId chargePointId) =>
        $"{TypeName}-{chargePointId}";

    public static Props Props(ChargePointId chargePointId, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new ChargePointActor(chargePointId, clock));

    private void Reserve(ReserveConnector reserve)
    {
        var replyTo = Sender;
        if (_state.Value is not Registered chargePoint
            || !chargePoint.Connectors.TryGetValue(reserve.ConnectorNumber, out var connector))
        {
            Decide(reserve);
            return;
        }

        if (connector.State.Value is Reserved held && held.TokenUid != reserve.TokenUid)
        {
            replyTo.Tell(new CommandRejected(
                "reserved-for-another-token", $"Connector {reserve.ConnectorNumber} is reserved."));
            return;
        }

        var now = _clock.GetUtcNow();
        var reserved = new ConnectorReserved(
            reserve.ChargePointId, reserve.ConnectorNumber, reserve.TokenUid, now.AddMinutes(15))
        {
            CommandId = reserve.CommandId,
            OccurredAt = now,
        };
        Persist(reserved, @event =>
        {
            _state = ChargePoint.Evolve(_state, @event);
            replyTo.Tell(new CommandAccepted(LastSequenceNr));
        });
    }

    private void Decide(ChargePointCommand command)
    {
        var replyTo = Sender;
        switch (ChargePoint.Decide(_state, command, DecisionContext.From(_clock)))
        {
            case Ok<ImmutableArray<ChargePointEvent>> { Value.IsEmpty: true }:
                replyTo.Tell(new CommandAccepted(LastSequenceNr));
                break;
            case Ok<ImmutableArray<ChargePointEvent>> accepted:
                PersistAll(
                    accepted.Value,
                    @event => _state = ChargePoint.Evolve(_state, @event),
                    () => replyTo.Tell(new CommandAccepted(LastSequenceNr)));
                break;
            case Err<ChargePointError> rejected:
                replyTo.Tell(rejected.Error.ToReply());
                break;
            case null:
                throw new InvalidOperationException("Decide returned a default Result.");
        }
    }
}
