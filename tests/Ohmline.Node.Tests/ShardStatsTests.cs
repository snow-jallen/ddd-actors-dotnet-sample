using System.Diagnostics.Metrics;
using Akka.DependencyInjection;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ohmline.ActorShell.Telemetry;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.Testing.Metrics;

namespace Ohmline.Node.Tests;

/// <summary>
/// The shard regions' size, as the node's poller reports it (section 18.2): asked of the
/// regions on a timer, kept per type name, and read when the gauges are collected.
/// </summary>
public sealed class ShardStatsTests(ITestOutputHelper output) : ShardedNode(output)
{
    private static readonly SiteId Depot = new("slc-depot");

    protected override void ConfigureAkka(
        AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        base.ConfigureAkka(builder, provider);
        builder.WithShardStats(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task Given_charge_points_in_their_region_then_the_gauges_count_them_by_type()
    {
        using var recorded = new InstrumentRecorder(
            DependencyResolver.For(Sys).Resolver.GetService<IMeterFactory>());

        foreach (var number in (int[])[1, 2, 3])
        {
            var chargePoint = new ChargePointId($"OHM-SLC-{number:D4}");
            (await AskAsync<ChargePointRegion, CommandReply>(new RegisterChargePoint(
                    CommandId.New(Clock), chargePoint, Depot, [new Power(22m)])))
                .ShouldBeOfType<CommandAccepted>();
        }

        await AwaitAssertAsync(
            () =>
            {
                recorded.Observe();
                Last(recorded, ActorShellMetrics.ShardingEntities, "charge-point").ShouldBe(3);
                Last(recorded, ActorShellMetrics.ShardingShards, "charge-point")
                    .ShouldBeInRange(1, 3);
            },
            Patience,
            cancellationToken: Ct);
    }

    private static double Last(InstrumentRecorder recorded, string gauge, string typeName) =>
        recorded.Of(gauge)
            .Last(figure => figure.Tags[ActorShellMetrics.ShardTypeTag] == typeName)
            .Value;
}
