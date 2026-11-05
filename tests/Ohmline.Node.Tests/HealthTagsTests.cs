using Ohmline.ActorShell.Health;
using Ohmline.ServiceDefaults;

namespace Ohmline.Node.Tests;

/// <summary>
/// The shell tags its checks and the service defaults map the endpoints, each knowing nothing
/// of the other (section 18.5): the tags must still be the same strings.
/// </summary>
public sealed class HealthTagsTests
{
    [Fact]
    public void The_shells_tags_are_the_ones_the_health_endpoints_run()
    {
        HealthTags.Live.ShouldBe(ServiceDefaultsExtensions.LiveTag);
        HealthTags.Ready.ShouldBe(ServiceDefaultsExtensions.ReadyTag);
    }
}
