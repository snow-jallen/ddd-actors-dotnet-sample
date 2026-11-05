using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.Tariffs.Contracts.Book;

namespace Ohmline.DeviceGateway.Connections;

/// <summary>
/// <c>/ocpp/{chargePointId}</c>: a charger connects with a WebSocket, and a
/// <see cref="ChargerConnectionActor"/> serves it for as long as the socket is open. The
/// socket's two directions run here, outside the actor: frames read are told to it, frames it
/// sends are written by the link.
/// </summary>
public static class ChargerSockets
{
    /// <summary>The WebSocket subprotocol a charger asks for.</summary>
    public const string Subprotocol = "ocpp1.6";

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapChargerSockets()
        {
            endpoints.Map("/ocpp/{chargePointId}", ConnectAsync);
            return endpoints;
        }
    }

    public static async Task<IResult> ConnectAsync(
        string chargePointId,
        HttpContext http,
        ActorSystem system,
        IRequiredActor<ChargePointRegion> chargePoints,
        IRequiredActor<ChargingSessionRegion> sessions,
        IRequiredActor<TariffBookProxy> tariffBook,
        IOptions<GatewayOptions> options,
        TimeProvider clock,
        ILogger<ChargerConnectionActor> log)
    {
        if (!http.WebSockets.IsWebSocketRequest)
        {
            return TypedResults.BadRequest("A charger connects with a WebSocket.");
        }

        if (ChargePointId.TryCreate(chargePointId) is not Ok<ChargePointId> { Value: var id })
        {
            return TypedResults.NotFound($"'{chargePointId}' is not a charge point id.");
        }

        var subprotocol = http.WebSockets.WebSocketRequestedProtocols.Contains(Subprotocol)
            ? Subprotocol
            : null;
        using var socket = await http.WebSockets.AcceptWebSocketAsync(subprotocol);
        var link = new WebSocketLink(socket);
        var regions = new ChargerRegions(
            chargePoints.ActorRef,
            sessions.ActorRef,
            tariffBook.ActorRef,
            DistributedPubSub.Get(system).Mediator);
        var connection = system.ActorOf(
            ChargerConnectionActor.Props(id, link, regions, options.Value, clock, log),
            $"charger-{id}-{Guid.NewGuid():N}");

        var sending = link.SendAllAsync(http.RequestAborted);
        await ReceiveAllAsync(socket, id, connection, link.Closing, http.RequestAborted);
        connection.Tell(Disconnected.Instance);
        link.Close();
        await Quietly(sending);

        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        }

        return TypedResults.Empty;
    }

    /// <summary>
    /// Each text message, rebuilt from its fragments, told to the actor in a trace of its own:
    /// the connection's request span lasts as long as the socket, hours, so each frame starts a
    /// new trace with a link to it (section 17.7), and the actor carries the frame's trace on.
    /// </summary>
    private static async Task ReceiveAllAsync(
        WebSocket socket,
        ChargePointId chargePointId,
        IActorRef connection,
        CancellationToken closing,
        CancellationToken aborted)
    {
        var connected = Activity.Current?.Context;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(closing, aborted);
        var buffer = new byte[16 * 1024];
        var message = new MemoryStream();
        try
        {
            while (socket.State is WebSocketState.Open)
            {
                var received = await socket.ReceiveAsync(buffer, stopping.Token);
                if (received.MessageType is WebSocketMessageType.Close)
                {
                    return;
                }

                message.Write(buffer, 0, received.Count);
                if (received.EndOfMessage)
                {
                    using (FrameSpans.Start(chargePointId, connected))
                    {
                        connection.TellTraced(
                            new FrameReceived(Encoding.UTF8.GetString(message.ToArray())));
                    }

                    message.SetLength(0);
                }
            }
        }
        catch (Exception closed) when (IsClosed(closed))
        {
            // The actor gave up on the charger, the request was aborted, or the socket broke.
        }
    }

    private static async Task Quietly(Task sending)
    {
        try
        {
            await sending;
        }
        catch (Exception closed) when (IsClosed(closed))
        {
            // Frames still queued for a socket that is gone go nowhere.
        }
    }

    private static bool IsClosed(Exception exception) =>
        exception is OperationCanceledException or WebSocketException;
}
