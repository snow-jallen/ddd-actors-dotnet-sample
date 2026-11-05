using Akka.TestKit.Xunit;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Actors.Budgets;
using Ohmline.SiteEnergy.Actors.Tests.Serialization;
using Ohmline.SiteEnergy.Domain.Budgets;
using Ohmline.Testing;
using static Ohmline.SiteEnergy.Actors.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Actors.Tests.Budgets;

/// <summary>A site's budget snapshot, written and read back, gives back an equal state.</summary>
public sealed class SitePowerBudgetSnapshotTests(ITestOutputHelper output)
    : TestKit(
        ActorTestConfig.With(Serializers.SiteEnergy), nameof(SitePowerBudgetSnapshotTests), output)
{
    [Fact]
    public void Given_an_unconfigured_site_then_there_is_nothing_to_snapshot() =>
        SitePowerBudgetState.Initial.ToSnapshot().ShouldBeNull();

    [Fact]
    public void Given_a_busy_curtailed_site_when_read_back_then_the_state_is_equal()
    {
        SitePowerBudgetState state = Configured.Of(SiteEnergySamples.Depot)
            .Open(new OpenRequest(Fleet1, BusSession, new Power(50m)), new Power(50m))
            .Open(new OpenRequest(Public2, CarSession, new Power(50m)), granted: null)
            with { Curtailment = new Power(120m) };
        var snapshot = state.ToSnapshot().ShouldNotBeNull();

        var bytes = Sys.Serialization.FindSerializerFor(snapshot).ToBinary(snapshot);
        var read = Sys.Serialization.Deserialize(
            bytes, 9102, "site-energy.site-power-budget-snapshot.v1");

        read.ShouldBeOfType<SitePowerBudgetSnapshot>().ToState().ShouldBe(state);
    }
}
