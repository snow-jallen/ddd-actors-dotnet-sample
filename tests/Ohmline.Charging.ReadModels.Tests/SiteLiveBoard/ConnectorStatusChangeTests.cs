using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.ReadModels.SiteLiveBoard;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.SiteLiveBoard;

/// <summary>The board's rules for charge point events, without a database.</summary>
public sealed class ConnectorStatusChangeTests
{
    public static TheoryData<ChargePointEvent, string> ConnectorEvents => new()
    {
        { new ConnectorReserved(Charger, 2, DriversCard, Morning.AddMinutes(15)), "reserved" },
        { new ReservationCancelled(Charger, 2, DriversCard), "available" },
        { new ReservationExpired(Charger, 2, DriversCard), "available" },
        { new ConnectorReleased(Charger, 2, Session), "available" },
        { new ConnectorFaulted(Charger, 2, "GroundFailure"), "faulted" },
        { new ConnectorFaultCleared(Charger, 2), "available" },
        { new ConnectorMadeUnavailable(Charger, 2), "unavailable" },
        { new ConnectorMadeAvailable(Charger, 2), "available" },
    };

    [Fact]
    public void A_registration_puts_each_connector_on_its_sites_board_as_available() =>
        ConnectorStatusChange.Of(
                new ChargePointRegistered(Charger, Depot, [new Power(22m), new Power(150m)]))
            .ShouldBe(
            [
                new ConnectorStatusChange("OHM-SLC-0042/1", "slc-depot", "available", null),
                new ConnectorStatusChange("OHM-SLC-0042/2", "slc-depot", "available", null),
            ]);

    [Theory]
    [MemberData(nameof(ConnectorEvents))]
    public void A_connector_event_changes_that_connectors_status(
        ChargePointEvent @event, string status) =>
        ConnectorStatusChange.Of(@event).ShouldBe(
            [new ConnectorStatusChange("OHM-SLC-0042/2", SiteId: null, status, SessionId: null)]);

    [Fact]
    public void An_occupied_connector_shows_its_session() =>
        ConnectorStatusChange.Of(new ConnectorOccupied(Charger, 1, Session, DriversCard))
            .ShouldBe(
                [new ConnectorStatusChange("OHM-SLC-0042/1", null, "occupied", Session.Value)]);
}
