using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Ohmline.DeviceGateway.Ocpp.Frames;

namespace Ohmline.DeviceGateway.Connections;

/// <summary>
/// The connection actor's side of a charger's WebSocket: frames are queued here and written by
/// one loop, so the actor never waits for the network and two frames never interleave.
/// </summary>
public sealed class WebSocketLink(WebSocket socket) : IChargerLink
{
    /// <summary>How long a charger has to answer the close frame of a restart.</summary>
    public static readonly TimeSpan CloseHandshakeTimeout = TimeSpan.FromSeconds(2);

    private readonly Channel<string> _outgoing = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _closing = new();
    private string? _restart;

    /// <summary>Cancelled when the actor gives up on the charger.</summary>
    public CancellationToken Closing => _closing.Token;

    public void Send(string frame) => _outgoing.Writer.TryWrite(frame);

    public void Close()
    {
        _outgoing.Writer.TryComplete();
        _closing.Cancel();
    }

    /// <summary>
    /// The gateway is stopping (section 21.7): the frames already queued are written, then a
    /// close frame that asks the charger to reconnect after <paramref name="reconnectAfter"/>
    /// (<see cref="ReconnectHint"/>). The charger's own close frame then ends the connection, as
    /// any close from a charger does.
    /// </summary>
    public void Restart(TimeSpan reconnectAfter)
    {
        Volatile.Write(ref _restart, ReconnectHint.Describe(reconnectAfter));
        _outgoing.Writer.TryComplete();
    }

    /// <summary>
    /// Writes every queued frame, in order, until the link is closed; for a restart, the close
    /// frame last, from this same loop, so it never interleaves with a frame.
    /// </summary>
    public async Task SendAllAsync(CancellationToken aborted)
    {
        await foreach (var frame in _outgoing.Reader.ReadAllAsync(aborted))
        {
            if (socket.State is not WebSocketState.Open)
            {
                return;
            }

            await socket.SendAsync(
                Encoding.UTF8.GetBytes(frame), WebSocketMessageType.Text, true, aborted);
        }

        if (Volatile.Read(ref _restart) is { } hint && socket.State is WebSocketState.Open)
        {
            await socket.CloseOutputAsync(ReconnectHint.ServiceRestart, hint, aborted);
            // A charger that never answers is cut off, rather than holding up the shutdown.
            _closing.CancelAfter(CloseHandshakeTimeout);
        }
    }
}
