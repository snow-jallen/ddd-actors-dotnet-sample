using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

public sealed class ConnectorTests
{
    private static readonly ChargePointId ChargePoint = new("OHM-SLC-0042");

    private static readonly Connector Available =
        new(new ConnectorId(ChargePoint, 1), new Power(22m), ConnectorState.Available);

    [Fact]
    public void Given_a_connector_whose_state_changes_then_it_is_still_the_same_connector()
    {
        var occupied = Available with { State = new ConnectorState("Occupied") };

        occupied.ShouldNotBe(Available);
        ConnectorComparer.ById.Equals(occupied, Available).ShouldBeTrue();
        ConnectorComparer.ById.GetHashCode(occupied)
            .ShouldBe(ConnectorComparer.ById.GetHashCode(Available));
    }

    [Fact]
    public void Given_equal_values_on_another_charge_point_then_it_is_a_different_connector()
    {
        var otherChargePoint = new ChargePointId("OHM-SLC-0043");
        var elsewhere = Available with { Id = new ConnectorId(otherChargePoint, 1) };

        ConnectorComparer.ById.Equals(elsewhere, Available).ShouldBeFalse();
    }

    [Fact]
    public void Given_a_set_keyed_by_identity_then_one_connector_is_held_once()
    {
        var derated = Available with { MaxPower = new Power(11m) };

        HashSet<Connector> connectors = [with(ConnectorComparer.ById), Available, derated];

        connectors.Count.ShouldBe(1);
        connectors.Single().MaxPower.ShouldBe(new Power(22m));
    }

    [Fact]
    public void Given_missing_connectors_then_the_comparer_handles_null()
    {
        ConnectorComparer.ById.Equals(null, null).ShouldBeTrue();
        ConnectorComparer.ById.Equals(Available, null).ShouldBeFalse();
    }
}
