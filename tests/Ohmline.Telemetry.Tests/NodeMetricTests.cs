using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Telemetry;
using Ohmline.Charging.Actors.Diagnostics;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// The node's own wiring (section 18.3): a collector on the factory of the node's container
/// sees what the node's entities record, so the instruments, their registration and the
/// shell's extension are tested together, as they run.
/// </summary>
[Trait("Category", "Integration")]
public sealed class NodeMetricTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static int _next = Random.Shared.Next(1000, 9000);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_command_over_HTTP_then_the_nodes_meters_count_it_and_time_recovery()
    {
        await using var node = new TracedNode(postgres);
        var meters = node.Services.GetRequiredService<IMeterFactory>();
        using var decided = new MetricCollector<long>(
            meters, ChargingDiagnostics.MeterName, ChargingMetrics.Commands);
        using var recovered = new MetricCollector<double>(
            meters, ActorShellDiagnostics.MeterName, ActorShellMetrics.RecoveryDuration);
        var chargePoint = $"OHM-MET-{Interlocked.Increment(ref _next):0000}";

        var registered = await node.CreateClient().PostAsJsonAsync(
            "/charge-points",
            new
            {
                chargePointId = chargePoint,
                siteId = "slc-depot",
                connectorMaxPowersKw = new[] { 22m },
            },
            Ct);

        registered.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await decided.WaitForMeasurementsAsync(1, Patience);
        decided.GetMeasurementSnapshot()
            .ContainsTags(
                new KeyValuePair<string, object?>(ChargingMetrics.AggregateTag, "charge-point"),
                new KeyValuePair<string, object?>(
                    ChargingMetrics.CommandTag, "RegisterChargePoint"))
            .ShouldHaveSingleItem();
        recovered.GetMeasurementSnapshot()
            .ContainsTags(new KeyValuePair<string, object?>(
                ActorShellMetrics.ActorTypeTag, "ChargePointActor"))
            .ShouldNotBeEmpty();
    }
}
