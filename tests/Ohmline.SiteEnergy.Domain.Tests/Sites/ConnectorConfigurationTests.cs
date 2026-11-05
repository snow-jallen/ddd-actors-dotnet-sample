using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Domain.Tests.Sites;

public sealed class ConnectorConfigurationTests
{
    private static readonly ConnectorId Connector = new(new ChargePointId("OHM-SLC-0105"), 1);

    [Fact]
    public void Given_a_minimum_within_the_maximum_when_trying_then_it_is_a_configuration()
    {
        var result = ConnectorConfiguration.TryCreate(
            Connector, new Power(7.4m), new Power(22m), new Public());

        result.IsOk.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_minimum_above_the_maximum_when_trying_then_it_is_out_of_range()
    {
        var result = ConnectorConfiguration.TryCreate(
            Connector, new Power(22m), new Power(7.4m), new Public());

        result.ShouldBe(Result.Fail<ValidationError>(
            new OutOfRange("Minimum power", 22m, "at most 7.4 kW")));
    }

    [Fact]
    public void Given_no_maximum_power_when_trying_then_it_is_out_of_range()
    {
        var result = ConnectorConfiguration.TryCreate(
            Connector, Power.Zero, Power.Zero, new Public());

        result.ShouldBe(Result.Fail<ValidationError>(
            new OutOfRange("Maximum power", 0m, "more than 0 kW")));
    }

    [Fact]
    public void Given_priority_classes_then_each_class_is_one_value()
    {
        PriorityClass fleet = new Fleet();

        fleet.ShouldBe(new Fleet());
        fleet.ShouldNotBe(new Public());
        Rank(fleet).ShouldBeLessThan(Rank(new Public()));
    }

    // Closed: the switch needs no discard, and a new class would not compile here (CS8509).
    private static int Rank(PriorityClass priority) => priority switch
    {
        Fleet => 0,
        Public => 1,
    };
}
