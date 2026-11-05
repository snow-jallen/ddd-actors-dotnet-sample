using System.Collections.Immutable;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests;

/// <summary>
/// What every event records besides what happened: the command that caused it and when.
/// </summary>
public sealed class DomainEventTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly ConnectorId Connector = new(Charger, 1);

    [Fact]
    public void Given_a_decision_with_two_events_then_each_records_the_command_and_the_time()
    {
        var reserved = ChargePoint.Fold(
        [
            new ChargePointRegistered(Charger, Depot, [new Power(50m)]),
            new ConnectorReserved(Charger, 1, DriversCard, Morning.AddMinutes(15)),
        ]);
        var reserve = new ReserveConnector(
            new CommandId(Guid.CreateVersion7(Noon)), Charger, 1, SomeoneElsesCard);

        var events = Accepted(ChargePoint.Decide(reserved, reserve, new DecisionContext(Noon)));

        events.Select(@event => @event.GetType().Name)
            .ShouldBe(["ReservationExpired", "ConnectorReserved"]);
        events.ShouldAllBe(@event =>
            @event.CommandId == reserve.CommandId && @event.OccurredAt == Noon);
    }

    [Fact]
    public void Given_the_same_command_in_the_same_state_and_context_then_the_events_are_equal()
    {
        var active = ChargingSession.Fold([Started()]);
        var stop = new StopSession(Command, Session);

        var first = Accepted(ChargingSession.Decide(active, stop, new DecisionContext(Noon)));
        var second = Accepted(ChargingSession.Decide(active, stop, new DecisionContext(Noon)));

        second.ShouldBe(first);
    }

    [Fact]
    public void Given_a_suspension_when_replayed_then_the_session_is_paused_since_it_occurred()
    {
        var paused = ChargingSession.Fold(
            [Started(), new ChargingSuspended(Session).At(Morning.AddMinutes(48))]);

        paused.Value.ShouldBeOfType<Suspended>().Since.ShouldBe(Morning.AddMinutes(48));
    }

    [Fact]
    public void Given_a_session_that_ends_then_the_event_carries_what_its_reactions_need()
    {
        var ended = Accepted(ChargingSession.Decide(
                ChargingSession.Fold([Started()]),
                new StopSession(Command, Session),
                new DecisionContext(Noon)))
            .ShouldHaveSingleItem().ShouldBeOfType<SessionEnded>();

        (ended.SiteId, ended.ConnectorId, ended.TokenUid).ShouldBe((Depot, Connector, DriversCard));
        ended.OccurredAt.ShouldBe(Noon);
    }

    private static SessionStarted Started() =>
        new(Session, Depot, Connector, DriversCard, StandardTariff(0.42m),
            new Money(40m, Currency.Usd), 1_200_000)
        {
            CommandId = Command,
            OccurredAt = Morning,
        };

    private static ImmutableArray<TEvent> Accepted<TEvent, TError>(
        Result<ImmutableArray<TEvent>, TError> decision) =>
        decision.Value.ShouldBeOfType<Ok<ImmutableArray<TEvent>>>().Value;
}
