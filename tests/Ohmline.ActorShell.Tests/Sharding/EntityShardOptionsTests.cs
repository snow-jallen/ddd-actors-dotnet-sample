using Akka.Cluster.Sharding;
using Ohmline.ActorShell.Sharding;

namespace Ohmline.ActorShell.Tests.Sharding;

public sealed class EntityShardOptionsTests
{
    [Fact]
    public void Every_region_of_aggregates_starts_from_the_same_written_down_choices()
    {
        var options = EntityShardOptions.Create();

        options.Role.ShouldBe("charging");
        options.StateStoreMode.ShouldBe(StateStoreMode.DData);
        options.HandOffStopMessage.ShouldBe(Stop.Instance);
        options.PassivateIdleEntityAfter.ShouldBe(TimeSpan.FromMinutes(2));
        options.RememberEntities.ShouldBe(false);
    }

    [Fact]
    public void Each_region_gets_options_of_its_own() =>
        EntityShardOptions.Create().ShouldNotBeSameAs(EntityShardOptions.Create());
}
