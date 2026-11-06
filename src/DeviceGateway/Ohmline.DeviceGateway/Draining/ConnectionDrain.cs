using Akka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ohmline.DeviceGateway.Connections;
using Ohmline.DeviceGateway.Diagnostics;

namespace Ohmline.DeviceGateway.Draining;

/// <summary>
/// Closes this gateway node's charger connections on its way out (section 21.7), as
/// <see cref="DrainPlan"/> lays them out, each with a reconnect hint, and waits until they have
/// closed. Runs as a coordinated-shutdown task in the phase <c>service-unbind</c>: before the
/// node leaves the cluster, so the chargers' last frames still reach Charging.
/// </summary>
public sealed class ConnectionDrain(
    OpenConnections connections,
    IOptions<GatewayOptions> options,
    TimeProvider clock,
    ILogger<ConnectionDrain> log)
{
    public async Task<Done> DrainAsync(CancellationToken cancellationToken = default)
    {
        connections.StopAccepting();
        var plan = DrainPlan.For(connections.Current, options.Value, Random.Shared);
        log.Draining(plan.Length, options.Value.DrainWindow);

        var started = clock.GetTimestamp();
        foreach (var batch in plan.GroupBy(step => step.CloseAt))
        {
            var wait = batch.Key - clock.GetElapsedTime(started);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, clock, cancellationToken);
            }

            foreach (var step in batch)
            {
                step.Connection.Restart(step.ReconnectAfter);
            }
        }

        await Task.WhenAll(plan.Select(step => step.Connection.Closed))
            .WaitAsync(cancellationToken);
        log.Drained(plan.Length, clock.GetElapsedTime(started));
        return Done.Instance;
    }
}
