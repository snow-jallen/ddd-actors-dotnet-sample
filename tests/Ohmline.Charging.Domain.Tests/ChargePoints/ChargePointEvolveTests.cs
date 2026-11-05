using System.Collections.Immutable;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

public sealed class ChargePointEvolveTests
{
    private static readonly DateTimeOffset Expiry = Morning.AddMinutes(15);

    private static readonly ChargePointRegistered Registration =
        new(Charger, Depot, [new Power(50m), new Power(22m)]);

    /// <summary>One event of every type, in an order a real charge point could produce.</summary>
    private static readonly ImmutableArray<ChargePointEvent> OneOfEach =
    [
        Registration,
        new ConnectorReserved(Charger, 1, DriversCard, Expiry),
        new ReservationCancelled(Charger, 1, DriversCard),
        new ConnectorReserved(Charger, 1, SomeoneElsesCard, Expiry.AddHours(1)),
        new ReservationExpired(Charger, 1, SomeoneElsesCard),
        new ConnectorOccupied(Charger, 1, Session, DriversCard),
        new ConnectorReleased(Charger, 1, Session),
        new ConnectorOutOfOrder(Charger, 2, "GroundFailure"),
        new ConnectorRepaired(Charger, 2),
        new ConnectorMadeUnavailable(Charger, 2),
        new ConnectorMadeAvailable(Charger, 2),
        new ConnectorOccupied(Charger, 2, NextSession, SomeoneElsesCard),
    ];

    [Fact]
    public void Given_no_events_when_folded_then_the_charge_point_is_unregistered()
    {
        ChargePoint.Fold([]).ShouldBe(ChargePointState.Initial);
    }

    [Fact]
    public void Given_a_registration_when_evolved_then_every_connector_is_available()
    {
        ChargePoint.Evolve(ChargePointState.Initial, Registration).ShouldBe(Registered(
            Connector(1, 50m, new Available()), Connector(2, 22m, new Available())));
    }

    [Theory]
    [InlineData("reserved")]
    [InlineData("cancelled")]
    [InlineData("expired")]
    [InlineData("occupied")]
    [InlineData("released")]
    [InlineData("out of order")]
    [InlineData("repaired")]
    [InlineData("made unavailable")]
    [InlineData("made available")]
    public void Given_a_connector_event_when_evolved_then_the_connector_takes_its_next_state(
        string happened)
    {
        (ChargePointEvent Event, ConnectorState Next) row = happened switch
        {
            "reserved" => (new ConnectorReserved(Charger, 1, DriversCard, Expiry),
                new Reserved(DriversCard, Expiry)),
            "cancelled" => (new ReservationCancelled(Charger, 1, DriversCard), new Available()),
            "expired" => (new ReservationExpired(Charger, 1, DriversCard), new Available()),
            "occupied" => (new ConnectorOccupied(Charger, 1, Session, DriversCard),
                new Occupied(Session)),
            "released" => (new ConnectorReleased(Charger, 1, Session), new Available()),
            "out of order" => (new ConnectorOutOfOrder(Charger, 1, "GroundFailure"),
                new OutOfOrder("GroundFailure")),
            "repaired" => (new ConnectorRepaired(Charger, 1), new Available()),
            "made unavailable" => (new ConnectorMadeUnavailable(Charger, 1), new Unavailable()),
            "made available" => (new ConnectorMadeAvailable(Charger, 1), new Available()),
            _ => throw new ArgumentOutOfRangeException(nameof(happened)),
        };

        ChargePoint.Fold([Registration, row.Event]).ShouldBe(Registered(
            Connector(1, 50m, row.Next), Connector(2, 22m, new Available())));
    }

    [Fact]
    public void Given_one_event_of_every_type_then_the_history_covers_the_whole_hierarchy()
    {
        var everyType = typeof(ChargePointEvent).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ChargePointEvent)) && !type.IsAbstract);

        OneOfEach.Select(@event => @event.GetType()).Distinct()
            .ShouldBe(everyType, ignoreOrder: true);
    }

    [Fact]
    public void Given_one_event_of_every_type_when_folded_then_each_connector_shows_its_last()
    {
        ChargePoint.Fold(OneOfEach).ShouldBe(Registered(
            Connector(1, 50m, new Available()), Connector(2, 22m, new Occupied(NextSession))));
    }

    [Fact]
    public void Given_events_that_decide_never_produces_when_evolved_then_nothing_fails()
    {
        var registered = ChargePoint.Fold([Registration]);

        ChargePoint.Evolve(ChargePointState.Initial, new ConnectorReleased(Charger, 1, Session))
            .ShouldBe(ChargePointState.Initial);
        ChargePoint.Evolve(registered, Registration with { ConnectorMaxPowers = [] })
            .ShouldBe(registered);
        ChargePoint.Evolve(registered, new ConnectorOutOfOrder(Charger, 9, "GroundFailure"))
            .ShouldBe(registered);
        ChargePoint.Evolve(registered, new ConnectorReleased(Charger, 1, Session))
            .ShouldBe(registered);
    }

    [Fact]
    public void Given_a_reservation_made_this_morning_when_replayed_later_then_its_expiry_is_kept()
    {
        var decided = ChargePoint.Decide(
            ChargePoint.Fold([Registration]),
            new ReserveConnector(Command, Charger, 1, DriversCard),
            new DecisionContext(Morning));
        var events = decided.Value.ShouldBeOfType<Ok<ImmutableArray<ChargePointEvent>>>().Value;

        ChargePoint.Fold([Registration, .. events]).ShouldBe(Registered(
            Connector(1, 50m, new Reserved(DriversCard, Expiry)),
            Connector(2, 22m, new Available())));
    }

    [Fact]
    public void Given_commands_handled_one_by_one_when_their_events_are_folded_then_all_agree()
    {
        ChargePointCommand[] commands =
        [
            new RegisterChargePoint(Command, Charger, Depot, [new(50m), new(22m)]),
            new ReserveConnector(Command, Charger, 1, DriversCard),
            new OccupyConnector(Command, Charger, 1, Session, DriversCard),
            new ReportConnectorOutOfOrder(Command, Charger, 2, "GroundFailure"),
            new OccupyConnector(Command, Charger, 2, NextSession, SomeoneElsesCard),
            new ReleaseConnector(Command, Charger, 1),
        ];
        var live = ChargePointState.Initial;
        var journal = new List<ChargePointEvent>();

        foreach (var command in commands)
        {
            if (ChargePoint.Decide(live, command, new DecisionContext(Morning))
                is Ok<ImmutableArray<ChargePointEvent>> accepted)
            {
                journal.AddRange(accepted.Value);
                live = accepted.Value.Aggregate(live, ChargePoint.Evolve);
            }
        }

        journal.Count.ShouldBe(5);
        ChargePoint.Fold(journal).ShouldBe(live);
    }

    private static Registered Registered(params Connector[] connectors) =>
        new(Charger, Depot, connectors.ToImmutableDictionary(connector => connector.Id.Number));

    private static Connector Connector(int number, decimal maxKilowatts, ConnectorState state) =>
        new(new ConnectorId(Charger, number), new Power(maxKilowatts), state);
}
