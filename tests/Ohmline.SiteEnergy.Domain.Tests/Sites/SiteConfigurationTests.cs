using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Domain.Tests.Sites;

public sealed class SiteConfigurationTests
{
    private static readonly SiteId Depot = new("slc-depot");
    private static readonly ChargePointId ChargePoint = new("OHM-SLC-0041");

    [Fact]
    public void Given_a_grid_limit_and_a_margin_then_the_configured_capacity_is_what_remains()
    {
        var site = Site(new Power(200m), new Power(10m), Connector(1, 50m));

        site.ConfiguredCapacity.ShouldBe(new Power(190m));
    }

    [Fact]
    public void Given_a_margin_as_large_as_the_grid_limit_when_trying_then_it_is_out_of_range()
    {
        var result = SiteConfiguration.TryCreate(
            Depot, new Power(60m), new Power(60m), "America/Denver", []);

        result.ShouldBe(Result.Fail<ValidationError>(
            new OutOfRange("Safety margin", 60m, "below the grid limit of 60 kW")));
    }

    [Fact]
    public void Given_a_connector_above_the_configured_capacity_when_trying_then_it_is_refused()
    {
        var result = SiteConfiguration.TryCreate(
            Depot, new Power(60m), new Power(5m), "America/Denver", [Connector(1, 150m)]);

        result.ShouldBe(Result.Fail<ValidationError>(new OutOfRange(
            "Maximum power of OHM-SLC-0041/1", 150m, "at most the configured capacity of 55 kW")));
    }

    [Fact]
    public void Given_one_connector_listed_twice_when_trying_then_it_is_malformed()
    {
        var result = SiteConfiguration.TryCreate(
            Depot, new Power(200m), new Power(10m), "America/Denver",
            [Connector(1, 50m), Connector(1, 22m)]);

        result.ShouldBe(Result.Fail<ValidationError>(
            new Malformed("Connectors", "OHM-SLC-0041/1", "each connector listed once")));
    }

    [Fact]
    public void Given_no_time_zone_when_trying_then_it_is_missing()
    {
        SiteConfiguration.TryCreate(Depot, new Power(200m), new Power(10m), " ", [])
            .ShouldBe(Result.Fail<ValidationError>(new Missing("Time zone")));
    }

    [Fact]
    public void Given_two_configurations_with_the_same_values_then_they_are_equal()
    {
        var first = Site(new Power(200m), new Power(10m), Connector(1, 50m), Connector(2, 50m));
        var second = Site(new Power(200m), new Power(10m), Connector(1, 50m), Connector(2, 50m));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(Site(new Power(200m), new Power(10m), Connector(1, 50m)));
    }

    [Fact]
    public void Given_invalid_values_when_created_as_known_good_then_it_throws()
    {
        Should.Throw<ArgumentException>(() => Site(new Power(10m), new Power(20m)));
    }

    private static SiteConfiguration Site(
        Power gridLimit, Power safetyMargin, params ConnectorConfiguration[] connectors) =>
        SiteConfiguration.Create(Depot, gridLimit, safetyMargin, "America/Denver", connectors);

    private static ConnectorConfiguration Connector(int number, decimal maxKilowatts) =>
        ConnectorConfiguration.Create(
            new ConnectorId(ChargePoint, number), new Power(6m), new Power(maxKilowatts),
            new Fleet());
}
