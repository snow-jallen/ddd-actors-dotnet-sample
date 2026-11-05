using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace Ohmline.DeviceGateway.Connections;

/// <summary>
/// The connection actor's side of a charger's WebSocket: frames are queued here and written by
/// one loop, so the actor never waits for the network and two frames never interleave.
/// </summary>
public sealed class WebSocketLink(WebSocket socket) : IChargerLink
{
    private readonly Channel<string> _outgoing = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _closing = new();

    /// <summary>Cancelled when the actor gives up on the charger.</summary>
    public CancellationToken Closing => _closing.Token;

    public void Send(string frame) => _outgoing.Writer.TryWrite(frame);

    public void Close()
    {
        _outgoing.Writer.TryComplete();
        _closing.Cancel();
    }

    /// <summary>Writes every queued frame, in order, until the link is closed.</summary>
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
    }
}
