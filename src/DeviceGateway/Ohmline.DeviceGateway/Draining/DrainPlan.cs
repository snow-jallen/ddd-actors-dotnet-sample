using System.Collections.Immutable;

namespace Ohmline.DeviceGateway.Draining;

/// <summary>
/// One connection's place in a drain: when, after the drain begins, it is closed, and how long
/// its charger is asked to wait before it reconnects.
/// </summary>
public sealed record DrainStep<TConnection>(
    TConnection Connection, TimeSpan CloseAt, TimeSpan ReconnectAfter);

/// <summary>
/// The order and the moments in which a stopping gateway closes its chargers' connections
/// (section 21.7). Closing them all at once would send them all to the other gateways at once:
/// a reconnection storm of boot notifications, status notifications and session lookups. So the
/// connections are shuffled, cut into batches, and each batch is closed at a jittered moment of
/// its own slot of the window; and each charger is asked to wait a jittered time of its own
/// before it comes back. A pure function: the randomness comes from the caller.
/// </summary>
public static class DrainPlan
{
    public static ImmutableArray<DrainStep<TConnection>> For<TConnection>(
        IReadOnlyCollection<TConnection> connections, GatewayOptions options, Random random)
    {
        if (connections.Count == 0)
        {
            return [];
        }

        var shuffled = connections.ToArray();
        random.Shuffle(shuffled);
        var batches = shuffled.Chunk(options.DrainBatchSize).ToArray();
        var slot = options.DrainWindow / batches.Length;
        var spread = options.ReconnectAfterMax - options.ReconnectAfterMin;

        var steps = ImmutableArray.CreateBuilder<DrainStep<TConnection>>(shuffled.Length);
        for (var batch = 0; batch < batches.Length; batch++)
        {
            // Somewhere in the first half of the batch's slot: batches never overlap.
            var closeAt = slot * batch + slot * (random.NextDouble() / 2);
            foreach (var connection in batches[batch])
            {
                var reconnectAfter = options.ReconnectAfterMin + spread * random.NextDouble();
                steps.Add(new DrainStep<TConnection>(
                    connection, closeAt, WholeSeconds(reconnectAfter)));
            }
        }

        return steps.MoveToImmutable();
    }

    /// <summary>The hint is in whole seconds: never shorter than the wait it stands for.</summary>
    private static TimeSpan WholeSeconds(TimeSpan wait) =>
        TimeSpan.FromSeconds(Math.Ceiling(wait.TotalSeconds));
}
