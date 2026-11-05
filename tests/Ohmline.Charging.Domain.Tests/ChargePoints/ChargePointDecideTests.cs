using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Deciders;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

/// <summary>
/// One test per row of the decision table in <see cref="ChargePoint.Decide"/>, as Given-When-Then:
/// given the charge point's events, when it decides a command, then these events or this error.
/// Connector 1 is put in each state by the event that puts it there.
/// </summary>
public sealed class ChargePointDecideTests
{
    private static readonly DateTimeOffset Expiry = Morning.AddMinutes(15);

    private static readonly ChargePointRegistered Registration = new(Charger, Depot, [new(50m)]);

    // The registration and the charge point level

    [Fact]
    public void Given_an_unregistered_charge_point_when_registered_then_it_joins_the_network()
    {
        var register = new RegisterChargePoint(Command, Charger, Depot, [new(50m), new(22m)]);

        Given()
            .When(register)
            .Then(new ChargePointRegistered(Charger, Depot, [new(50m), new(22m)]).At(Morning));
    }

    [Fact]
    public void Given_no_registration_when_a_connector_command_arrives_then_it_is_not_registered()
    {
        Given()
            .When(new ReserveConnector(Command, Charger, 1, DriversCard))
            .ThenRejects(new ChargePointError.NotRegistered(Charger));
    }

    [Fact]
    public void Given_a_charge_point_when_registered_again_then_it_is_already_registered()
    {
        Given("available")
            .When(new RegisterChargePoint(Command, Charger, Depot, [new(50m)]))
            .ThenRejects(new ChargePointError.AlreadyRegistered(Charger));
    }

    [Fact]
    public void Given_a_charge_point_when_a_command_names_a_missing_connector_then_it_is_unknown()
    {
        Given("available")
            .When(new OccupyConnector(Command, Charger, 3, Session, DriversCard))
            .ThenRejects(new ChargePointError.UnknownConnector(3));
    }

    [Fact]
    public void Given_a_default_state_when_deciding_then_the_null_arm_reports_the_bug()
    {
        Should.Throw<InvalidOperationException>(
            () => ChargePoint.Decide(
                default, new ReleaseConnector(Command, Charger, 1), new DecisionContext(Morning)));
    }

    // ReserveConnector

    [Fact]
    public void Given_an_available_connector_when_reserved_then_it_is_held_for_fifteen_minutes()
    {
        Given("available")
            .When(new ReserveConnector(Command, Charger, 1, DriversCard))
            .Then(new ConnectorReserved(Charger, 1, DriversCard, Expiry).At(Morning));
    }

    [Fact]
    public void Given_another_time_in_the_context_when_reserved_then_the_expiry_moves_with_it()
    {
        var noon = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        Given("available").At(noon)
            .When(new ReserveConnector(Command, Charger, 1, DriversCard))
            .Then(new ConnectorReserved(Charger, 1, DriversCard, noon.AddMinutes(15)).At(noon));
    }

    [Fact]
    public void Given_the_drivers_reservation_when_the_driver_reserves_again_then_nothing_changes()
    {
        Given("reserved")
            .When(new ReserveConnector(Command, Charger, 1, DriversCard))
            .ThenNothingChanges();
    }

    [Fact]
    public void Given_someone_elses_reservation_when_reserved_then_the_driver_learns_until_when()
    {
        Given("reserved")
            .When(new ReserveConnector(Command, Charger, 1, SomeoneElsesCard))
            .ThenRejects<ChargePointError.ReservedForAnotherToken>()
            .Until.ShouldBe(Expiry);
    }

    [Fact]
    public void Given_an_expired_reservation_when_another_driver_reserves_then_it_expires_first()
    {
        var reserve = new ReserveConnector(Command, Charger, 1, SomeoneElsesCard);

        Given("reserved").At(Expiry)
            .When(reserve)
            .Then(
                new ReservationExpired(Charger, 1, DriversCard).At(Expiry),
                new ConnectorReserved(Charger, 1, SomeoneElsesCard, Expiry.AddMinutes(15))
                    .At(Expiry));
    }

    [Fact]
    public void Given_the_drivers_expired_reservation_when_reserved_again_then_a_new_one_starts()
    {
        var later = Expiry.AddMinutes(3);

        Given("reserved").At(later)
            .When(new ReserveConnector(Command, Charger, 1, DriversCard))
            .Then(
                new ReservationExpired(Charger, 1, DriversCard).At(later),
                new ConnectorReserved(Charger, 1, DriversCard, later.AddMinutes(15)).At(later));
    }

    [Theory]
    [InlineData("occupied")]
    [InlineData("unavailable")]
    public void Given_a_connector_that_is_in_use_or_out_of_use_when_reserved_then_it_is_unavailable(
        string state)
    {
        Given(state)
            .When(new ReserveConnector(Command, Charger, 1, DriversCard))
            .ThenRejects(new ChargePointError.ConnectorUnavailable(1));
    }

    [Fact]
    public void Given_a_faulted_connector_when_reserved_then_the_fault_is_reported()
    {
        Given("faulted")
            .When(new ReserveConnector(Command, Charger, 1, DriversCard))
            .ThenRejects(new ChargePointError.ConnectorFaulted(1, "GroundFailure"));
    }

    // CancelReservation

    [Fact]
    public void Given_the_drivers_reservation_when_cancelled_then_it_is_cancelled()
    {
        Given("reserved")
            .When(new CancelReservation(Command, Charger, 1, DriversCard))
            .Then(new ReservationCancelled(Charger, 1, DriversCard).At(Morning));
    }

    [Fact]
    public void Given_someone_elses_reservation_when_cancelled_then_it_is_refused()
    {
        Given("reserved")
            .When(new CancelReservation(Command, Charger, 1, SomeoneElsesCard))
            .ThenRejects(new ChargePointError.ReservedForAnotherToken(1, Expiry));
    }

    [Theory]
    [InlineData("available")]
    [InlineData("occupied")]
    [InlineData("faulted")]
    [InlineData("unavailable")]
    public void Given_no_reservation_when_cancelled_then_nothing_changes(string state)
    {
        Given(state)
            .When(new CancelReservation(Command, Charger, 1, DriversCard))
            .ThenNothingChanges();
    }

    // ExpireReservation

    [Fact]
    public void Given_a_reservation_whose_time_is_up_when_the_timer_fires_then_it_expires()
    {
        Given("reserved").At(Expiry)
            .When(new ExpireReservation(Command, Charger, 1))
            .Then(new ReservationExpired(Charger, 1, DriversCard).At(Expiry));
    }

    [Fact]
    public void Given_a_reservation_with_time_left_when_the_timer_fires_early_then_nothing_changes()
    {
        var expire = new ExpireReservation(Command, Charger, 1);

        Given("reserved").At(Expiry.AddSeconds(-1))
            .When(expire)
            .ThenNothingChanges();
    }

    [Theory]
    [InlineData("available")]
    [InlineData("occupied")]
    [InlineData("faulted")]
    [InlineData("unavailable")]
    public void Given_no_reservation_when_a_stale_timer_fires_then_nothing_changes(string state)
    {
        Given(state).At(Expiry)
            .When(new ExpireReservation(Command, Charger, 1))
            .ThenNothingChanges();
    }

    // OccupyConnector

    [Fact]
    public void Given_an_available_connector_when_a_car_plugs_in_then_the_session_occupies_it()
    {
        Given("available")
            .When(new OccupyConnector(Command, Charger, 1, Session, DriversCard))
            .Then(new ConnectorOccupied(Charger, 1, Session, DriversCard).At(Morning));
    }

    [Fact]
    public void Given_the_drivers_reservation_when_they_plug_in_then_the_session_occupies_it()
    {
        Given("reserved")
            .When(new OccupyConnector(Command, Charger, 1, Session, DriversCard))
            .Then(new ConnectorOccupied(Charger, 1, Session, DriversCard).At(Morning));
    }

    [Fact]
    public void Given_someone_elses_reservation_when_a_driver_plugs_in_then_it_is_refused()
    {
        var occupy = new OccupyConnector(Command, Charger, 1, Session, SomeoneElsesCard);

        Given("reserved")
            .When(occupy)
            .ThenRejects(new ChargePointError.ReservedForAnotherToken(1, Expiry));
    }

    [Fact]
    public void Given_an_expired_reservation_when_another_driver_plugs_in_then_they_may_charge()
    {
        var occupy = new OccupyConnector(Command, Charger, 1, NextSession, SomeoneElsesCard);
        var later = Expiry.AddMinutes(3);

        Given("reserved").At(later)
            .When(occupy)
            .Then(
                new ReservationExpired(Charger, 1, DriversCard).At(later),
                new ConnectorOccupied(Charger, 1, NextSession, SomeoneElsesCard).At(later));
    }

    [Fact]
    public void Given_an_occupied_connector_when_its_session_plugs_in_again_then_nothing_changes()
    {
        Given("occupied")
            .When(new OccupyConnector(Command, Charger, 1, Session, DriversCard))
            .ThenNothingChanges();
    }

    [Theory]
    [InlineData("occupied")]
    [InlineData("unavailable")]
    public void Given_a_busy_or_closed_connector_when_another_car_plugs_in_then_it_is_unavailable(
        string state)
    {
        Given(state)
            .When(new OccupyConnector(Command, Charger, 1, NextSession, SomeoneElsesCard))
            .ThenRejects(new ChargePointError.ConnectorUnavailable(1));
    }

    [Fact]
    public void Given_a_faulted_connector_when_a_car_plugs_in_then_the_fault_is_reported()
    {
        Given("faulted")
            .When(new OccupyConnector(Command, Charger, 1, Session, DriversCard))
            .ThenRejects(new ChargePointError.ConnectorFaulted(1, "GroundFailure"));
    }

    // ReleaseConnector

    [Fact]
    public void Given_an_occupied_connector_when_the_car_is_unplugged_then_the_session_is_named()
    {
        Given("occupied")
            .When(new ReleaseConnector(Command, Charger, 1))
            .Then(new ConnectorReleased(Charger, 1, Session).At(Morning));
    }

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    [InlineData("faulted")]
    [InlineData("unavailable")]
    public void Given_no_car_plugged_in_when_released_then_nothing_changes(string state)
    {
        Given(state)
            .When(new ReleaseConnector(Command, Charger, 1))
            .ThenNothingChanges();
    }

    // ReportConnectorFault and ClearConnectorFault

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    [InlineData("occupied")]
    [InlineData("unavailable")]
    public void Given_a_connector_that_is_not_faulted_when_a_fault_is_reported_then_it_is_faulted(
        string state)
    {
        Given(state)
            .When(new ReportConnectorFault(Command, Charger, 1, "PowerSwitchFailure"))
            .Then(new ConnectorFaulted(Charger, 1, "PowerSwitchFailure").At(Morning));
    }

    [Fact]
    public void Given_a_faulted_connector_when_a_fault_is_reported_again_then_nothing_changes()
    {
        Given("faulted")
            .When(new ReportConnectorFault(Command, Charger, 1, "GroundFailure"))
            .ThenNothingChanges();
    }

    [Fact]
    public void Given_a_faulted_connector_when_the_fault_is_cleared_then_it_is_cleared()
    {
        Given("faulted")
            .When(new ClearConnectorFault(Command, Charger, 1))
            .Then(new ConnectorFaultCleared(Charger, 1).At(Morning));
    }

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    [InlineData("occupied")]
    [InlineData("unavailable")]
    public void Given_a_connector_that_is_not_faulted_when_a_fault_is_cleared_then_nothing_changes(
        string state)
    {
        Given(state)
            .When(new ClearConnectorFault(Command, Charger, 1))
            .ThenNothingChanges();
    }

    // MakeConnectorUnavailable and MakeConnectorAvailable

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    public void Given_a_free_or_reserved_connector_when_made_unavailable_then_it_is_out_of_use(
        string state)
    {
        Given(state)
            .When(new MakeConnectorUnavailable(Command, Charger, 1))
            .Then(new ConnectorMadeUnavailable(Charger, 1).At(Morning));
    }

    [Fact]
    public void Given_an_unavailable_connector_when_made_unavailable_then_nothing_changes()
    {
        Given("unavailable")
            .When(new MakeConnectorUnavailable(Command, Charger, 1))
            .ThenNothingChanges();
    }

    [Fact]
    public void Given_an_occupied_connector_when_made_unavailable_then_it_is_refused()
    {
        Given("occupied")
            .When(new MakeConnectorUnavailable(Command, Charger, 1))
            .ThenRejects(new ChargePointError.ConnectorUnavailable(1));
    }

    [Fact]
    public void Given_a_faulted_connector_when_operators_change_its_use_then_the_fault_is_reported()
    {
        var fault = new ChargePointError.ConnectorFaulted(1, "GroundFailure");

        Given("faulted")
            .When(new MakeConnectorUnavailable(Command, Charger, 1))
            .ThenRejects(fault);
        Given("faulted")
            .When(new MakeConnectorAvailable(Command, Charger, 1))
            .ThenRejects(fault);
    }

    [Fact]
    public void Given_an_unavailable_connector_when_made_available_then_it_is_back_in_service()
    {
        Given("unavailable")
            .When(new MakeConnectorAvailable(Command, Charger, 1))
            .Then(new ConnectorMadeAvailable(Charger, 1).At(Morning));
    }

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    [InlineData("occupied")]
    public void Given_a_connector_in_service_when_made_available_then_nothing_changes(string state)
    {
        Given(state)
            .When(new MakeConnectorAvailable(Command, Charger, 1))
            .ThenNothingChanges();
    }

    /// <summary>A charge point whose history is given, deciding at the depot's morning.</summary>
    private static ChargePointSpec Given(params IEnumerable<ChargePointEvent> history) =>
        ChargePointSpec.Given(history).At(Morning);

    /// <summary>A registered charge point whose connector 1 is in the named state.</summary>
    private static ChargePointSpec Given(string connectorState)
    {
        ChargePointEvent[] connector = connectorState switch
        {
            "available" => [],
            "reserved" => [new ConnectorReserved(Charger, 1, DriversCard, Expiry)],
            "occupied" => [new ConnectorOccupied(Charger, 1, Session, DriversCard)],
            "faulted" => [new ConnectorFaulted(Charger, 1, "GroundFailure")],
            "unavailable" => [new ConnectorMadeUnavailable(Charger, 1)],
            _ => throw new ArgumentOutOfRangeException(nameof(connectorState)),
        };

        return Given([Registration, .. connector]);
    }
}
