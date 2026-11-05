using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

public sealed class ChargePointProtocolTests
{
    [Fact]
    public void Given_two_registrations_of_the_same_connectors_then_the_events_are_equal()
    {
        Power[] connectors = [new(50m), new(50m)];
        var first = new ChargePointRegistered(Charger, Depot, [.. connectors]);
        var second = new ChargePointRegistered(Charger, Depot, [new(50m), new(50m)]);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Given_registrations_of_different_connectors_then_the_events_differ()
    {
        var twoConnectors = new ChargePointRegistered(Charger, Depot, [new(50m), new(50m)]);
        var derated = new ChargePointRegistered(Charger, Depot, [new(50m), new(22m)]);
        var elsewhere = new ChargePointRegistered(Charger, new("slc-west-garage"), [new(50m)]);

        derated.ShouldNotBe(twoConnectors);
        elsewhere.ShouldNotBe(new ChargePointRegistered(Charger, Depot, [new(50m)]));
    }

    [Fact]
    public void Given_a_connector_command_then_it_names_its_charge_point_on_the_base()
    {
        ChargePointCommand command = new OccupyConnector(Command, Charger, 1, Session, DriversCard);

        command.ChargePointId.ShouldBe(Charger);
        command.ShouldBeAssignableTo<ConnectorCommand>().ConnectorNumber.ShouldBe(1);
    }

    [Fact]
    public void Given_an_event_about_a_connector_then_it_is_a_charge_point_event()
    {
        ChargePointEvent faulted = new ConnectorFaulted(Charger, 2, "GroundFailure");

        var connectorEvent = faulted.ShouldBeAssignableTo<ConnectorEvent>();
        connectorEvent.ChargePointId.ShouldBe(Charger);
        connectorEvent.ConnectorNumber.ShouldBe(2);
    }
}
