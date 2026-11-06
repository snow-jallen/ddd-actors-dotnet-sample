using System.Collections.Concurrent;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DeviceGateway.Connections;

/// <summary>
/// The chargers connected to this gateway node right now, for its drain (section 21.7). A
/// connection is added when its WebSocket is accepted and removed when it has closed; once the
/// drain has begun, no new one is accepted.
/// </summary>
public sealed class OpenConnections
{
    private readonly ConcurrentDictionary<OpenConnection, byte> _open = new();
    private volatile bool _draining;

    /// <summary>False from the moment the drain begins.</summary>
    public bool Accepting => !_draining;

    public IReadOnlyCollection<OpenConnection> Current => [.. _open.Keys];

    /// <summary>Records an accepted connection; disposing what it returns removes it.</summary>
    public OpenConnection Add(ChargePointId chargePointId, WebSocketLink link)
    {
        var connection = new OpenConnection(
            chargePointId, link, closed => _open.TryRemove(closed, out _));
        _open[connection] = 0;
        return connection;
    }

    public void StopAccepting() => _draining = true;
}

/// <summary>One charger's open connection, until it has closed.</summary>
public sealed class OpenConnection(
    ChargePointId chargePointId, WebSocketLink link, Action<OpenConnection> remove) : IDisposable
{
    private readonly TaskCompletionSource _closed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ChargePointId ChargePointId => chargePointId;

    /// <summary>Completes when the connection has closed, however it closed.</summary>
    public Task Closed => _closed.Task;

    /// <summary>Asks the charger to reconnect after this long, and closes the connection.</summary>
    public void Restart(TimeSpan reconnectAfter) => link.Restart(reconnectAfter);

    public void Dispose()
    {
        remove(this);
        _closed.TrySetResult();
    }
}
