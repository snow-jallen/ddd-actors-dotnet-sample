using System.Collections.Immutable;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

/// <summary>
/// One test per row of the decision table in <see cref="ChargePoint.Decide"/>. The states are
/// written down directly; section 7.5 adds Evolve, and later tests build state from events.
/// </summary>
public sealed class ChargePointDecideTests
{
    private static readonly DateTimeOffset Expiry = Morning.AddMinutes(15);

    // The registration and the charge point level

    [Fact]
    public void Given_an_unregistered_charge_point_when_registered_then_it_joins_the_network()
    {
        var register = new RegisterChargePoint(Charger, Depot, [new(50m), new(22m)]);

        Decide(ChargePointState.Initial, register)
            .ShouldAccept(new ChargePointRegistered(Charger, Depot, [new(50m), new(22m)]));
    }

    [Fact]
    public void Given_no_registration_when_a_connector_command_arrives_then_it_is_not_registered()
    {
        Decide(ChargePointState.Initial, new ReserveConnector(Charger, 1, DriversCard))
            .ShouldReject(new ChargePointError.NotRegistered(Charger));
    }

    [Fact]
    public void Given_a_charge_point_when_registered_again_then_it_is_already_registered()
    {
        Decide(With("available"), new RegisterChargePoint(Charger, Depot, [new(50m)]))
            .ShouldReject(new ChargePointError.AlreadyRegistered(Charger));
    }

    [Fact]
    public void Given_a_charge_point_when_a_command_names_a_missing_connector_then_it_is_unknown()
    {
        Decide(With("available"), new OccupyConnector(Charger, 3, Session, DriversCard))
            .ShouldReject(new ChargePointError.UnknownConnector(3));
    }

    [Fact]
    public void Given_a_default_state_when_deciding_then_the_null_arm_reports_the_bug()
    {
        Should.Throw<InvalidOperationException>(
            () => Decide(default, new ReleaseConnector(Charger, 1)));
    }

    // ReserveConnector

    [Fact]
    public void Given_an_available_connector_when_reserved_then_it_is_held_for_fifteen_minutes()
    {
        Decide(With("available"), new ReserveConnector(Charger, 1, DriversCard))
            .ShouldAccept(new ConnectorReserved(Charger, 1, DriversCard, Expiry));
    }

    [Fact]
    public void Given_another_time_in_the_context_when_reserved_then_the_expiry_moves_with_it()
    {
        var noon = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        Decide(With("available"), new ReserveConnector(Charger, 1, DriversCard), at: noon)
            .ShouldAccept(new ConnectorReserved(Charger, 1, DriversCard, noon.AddMinutes(15)));
    }

    [Fact]
    public void Given_the_drivers_reservation_when_the_driver_reserves_again_then_nothing_changes()
    {
        Decide(With("reserved"), new ReserveConnector(Charger, 1, DriversCard))
            .ShouldAccept();
    }

    [Fact]
    public void Given_someone_elses_reservation_when_reserved_then_the_driver_learns_until_when()
    {
        Decide(With("reserved"), new ReserveConnector(Charger, 1, SomeoneElsesCard))
            .ShouldReject(new ChargePointError.ReservedForAnotherToken(1, Expiry));
    }

    [Fact]
    public void Given_an_expired_reservation_when_another_driver_reserves_then_it_expires_first()
    {
        Decide(With("reserved"), new ReserveConnector(Charger, 1, SomeoneElsesCard), at: Expiry)
            .ShouldAccept(
                new ReservationExpired(Charger, 1, DriversCard),
                new ConnectorReserved(Charger, 1, SomeoneElsesCard, Expiry.AddMinutes(15)));
    }

    [Theory]
    [InlineData("occupied")]
    [InlineData("unavailable")]
    public void Given_a_connector_that_is_in_use_or_out_of_use_when_reserved_then_it_is_unavailable(
        string state)
    {
        Decide(With(state), new ReserveConnector(Charger, 1, DriversCard))
            .ShouldReject(new ChargePointError.ConnectorUnavailable(1));
    }

    [Fact]
    public void Given_a_faulted_connector_when_reserved_then_the_fault_is_reported()
    {
        Decide(With("faulted"), new ReserveConnector(Charger, 1, DriversCard))
            .ShouldReject(new ChargePointError.ConnectorFaulted(1, "GroundFailure"));
    }

    // CancelReservation

    [Fact]
    public void Given_the_drivers_reservation_when_cancelled_then_it_is_cancelled()
    {
        Decide(With("reserved"), new CancelReservation(Charger, 1, DriversCard))
            .ShouldAccept(new ReservationCancelled(Charger, 1, DriversCard));
    }

    [Fact]
    public void Given_someone_elses_reservation_when_cancelled_then_it_is_refused()
    {
        Decide(With("reserved"), new CancelReservation(Charger, 1, SomeoneElsesCard))
            .ShouldReject(new ChargePointError.ReservedForAnotherToken(1, Expiry));
    }

    [Theory]
    [InlineData("available")]
    [InlineData("occupied")]
    [InlineData("faulted")]
    [InlineData("unavailable")]
    public void Given_no_reservation_when_cancelled_then_nothing_changes(string state)
    {
        Decide(With(state), new CancelReservation(Charger, 1, DriversCard)).ShouldAccept();
    }

    // ExpireReservation

    [Fact]
    public void Given_a_reservation_whose_time_is_up_when_the_timer_fires_then_it_expires()
    {
        Decide(With("reserved"), new ExpireReservation(Charger, 1), at: Expiry)
            .ShouldAccept(new ReservationExpired(Charger, 1, DriversCard));
    }

    [Fact]
    public void Given_a_reservation_with_time_left_when_the_timer_fires_early_then_nothing_changes()
    {
        Decide(With("reserved"), new ExpireReservation(Charger, 1), at: Expiry.AddSeconds(-1))
            .ShouldAccept();
    }

    [Theory]
    [InlineData("available")]
    [InlineData("occupied")]
    [InlineData("faulted")]
    [InlineData("unavailable")]
    public void Given_no_reservation_when_a_stale_timer_fires_then_nothing_changes(string state)
    {
        Decide(With(state), new ExpireReservation(Charger, 1), at: Expiry).ShouldAccept();
    }

    // OccupyConnector

    [Fact]
    public void Given_an_available_connector_when_a_car_plugs_in_then_the_session_occupies_it()
    {
        Decide(With("available"), new OccupyConnector(Charger, 1, Session, DriversCard))
            .ShouldAccept(new ConnectorOccupied(Charger, 1, Session, DriversCard));
    }

    [Fact]
    public void Given_the_drivers_reservation_when_they_plug_in_then_the_session_occupies_it()
    {
        Decide(With("reserved"), new OccupyConnector(Charger, 1, Session, DriversCard))
            .ShouldAccept(new ConnectorOccupied(Charger, 1, Session, DriversCard));
    }

    [Fact]
    public void Given_someone_elses_reservation_when_a_driver_plugs_in_then_it_is_refused()
    {
        Decide(With("reserved"), new OccupyConnector(Charger, 1, Session, SomeoneElsesCard))
            .ShouldReject(new ChargePointError.ReservedForAnotherToken(1, Expiry));
    }

    [Fact]
    public void Given_an_expired_reservation_when_another_driver_plugs_in_then_they_may_charge()
    {
        var occupy = new OccupyConnector(Charger, 1, NextSession, SomeoneElsesCard);

        Decide(With("reserved"), occupy, at: Expiry.AddMinutes(3)).ShouldAccept(
            new ReservationExpired(Charger, 1, DriversCard),
            new ConnectorOccupied(Charger, 1, NextSession, SomeoneElsesCard));
    }

    [Fact]
    public void Given_an_occupied_connector_when_its_session_plugs_in_again_then_nothing_changes()
    {
        Decide(With("occupied"), new OccupyConnector(Charger, 1, Session, DriversCard))
            .ShouldAccept();
    }

    [Theory]
    [InlineData("occupied")]
    [InlineData("unavailable")]
    public void Given_a_busy_or_closed_connector_when_another_car_plugs_in_then_it_is_unavailable(
        string state)
    {
        Decide(With(state), new OccupyConnector(Charger, 1, NextSession, SomeoneElsesCard))
            .ShouldReject(new ChargePointError.ConnectorUnavailable(1));
    }

    [Fact]
    public void Given_a_faulted_connector_when_a_car_plugs_in_then_the_fault_is_reported()
    {
        Decide(With("faulted"), new OccupyConnector(Charger, 1, Session, DriversCard))
            .ShouldReject(new ChargePointError.ConnectorFaulted(1, "GroundFailure"));
    }

    // ReleaseConnector

    [Fact]
    public void Given_an_occupied_connector_when_the_car_is_unplugged_then_the_session_is_named()
    {
        Decide(With("occupied"), new ReleaseConnector(Charger, 1))
            .ShouldAccept(new ConnectorReleased(Charger, 1, Session));
    }

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    [InlineData("faulted")]
    [InlineData("unavailable")]
    public void Given_no_car_plugged_in_when_released_then_nothing_changes(string state)
    {
        Decide(With(state), new ReleaseConnector(Charger, 1)).ShouldAccept();
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
        Decide(With(state), new ReportConnectorFault(Charger, 1, "PowerSwitchFailure"))
            .ShouldAccept(new ConnectorFaulted(Charger, 1, "PowerSwitchFailure"));
    }

    [Fact]
    public void Given_a_faulted_connector_when_a_fault_is_reported_again_then_nothing_changes()
    {
        Decide(With("faulted"), new ReportConnectorFault(Charger, 1, "GroundFailure"))
            .ShouldAccept();
    }

    [Fact]
    public void Given_a_faulted_connector_when_the_fault_is_cleared_then_it_is_cleared()
    {
        Decide(With("faulted"), new ClearConnectorFault(Charger, 1))
            .ShouldAccept(new ConnectorFaultCleared(Charger, 1));
    }

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    [InlineData("occupied")]
    [InlineData("unavailable")]
    public void Given_a_connector_that_is_not_faulted_when_a_fault_is_cleared_then_nothing_changes(
        string state)
    {
        Decide(With(state), new ClearConnectorFault(Charger, 1)).ShouldAccept();
    }

    // MakeConnectorUnavailable and MakeConnectorAvailable

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    public void Given_a_free_or_reserved_connector_when_made_unavailable_then_it_is_out_of_use(
        string state)
    {
        Decide(With(state), new MakeConnectorUnavailable(Charger, 1))
            .ShouldAccept(new ConnectorMadeUnavailable(Charger, 1));
    }

    [Fact]
    public void Given_an_unavailable_connector_when_made_unavailable_then_nothing_changes()
    {
        Decide(With("unavailable"), new MakeConnectorUnavailable(Charger, 1)).ShouldAccept();
    }

    [Fact]
    public void Given_an_occupied_connector_when_made_unavailable_then_it_is_refused()
    {
        Decide(With("occupied"), new MakeConnectorUnavailable(Charger, 1))
            .ShouldReject(new ChargePointError.ConnectorUnavailable(1));
    }

    [Fact]
    public void Given_a_faulted_connector_when_operators_change_its_use_then_the_fault_is_reported()
    {
        var fault = new ChargePointError.ConnectorFaulted(1, "GroundFailure");

        Decide(With("faulted"), new MakeConnectorUnavailable(Charger, 1)).ShouldReject(fault);
        Decide(With("faulted"), new MakeConnectorAvailable(Charger, 1)).ShouldReject(fault);
    }

    [Fact]
    public void Given_an_unavailable_connector_when_made_available_then_it_is_back_in_service()
    {
        Decide(With("unavailable"), new MakeConnectorAvailable(Charger, 1))
            .ShouldAccept(new ConnectorMadeAvailable(Charger, 1));
    }

    [Theory]
    [InlineData("available")]
    [InlineData("reserved")]
    [InlineData("occupied")]
    public void Given_a_connector_in_service_when_made_available_then_nothing_changes(string state)
    {
        Decide(With(state), new MakeConnectorAvailable(Charger, 1)).ShouldAccept();
    }

    private static Result<ImmutableArray<ChargePointEvent>, ChargePointError> Decide(
        ChargePointState state, ChargePointCommand command, DateTimeOffset? at = null) =>
        ChargePoint.Decide(state, command, new DecisionContext(at ?? Morning));

    /// <summary>A registered charge point whose connector 1 is in the named state.</summary>
    private static ChargePointState With(string connectorState)
    {
        ConnectorState state = connectorState switch
        {
            "available" => new Available(),
            "reserved" => new Reserved(DriversCard, Expiry),
            "occupied" => new Occupied(Session),
            "faulted" => new Faulted("GroundFailure"),
            "unavailable" => new Unavailable(),
            _ => throw new ArgumentOutOfRangeException(nameof(connectorState)),
        };

        var connector = new Connector(new ConnectorId(Charger, 1), new Power(50m), state);
        return new Registered(Charger, Depot, ImmutableDictionary<int, Connector>.Empty
            .Add(1, connector));
    }
}
