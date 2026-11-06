using System.Net.WebSockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.DeviceGateway.Connections;
using Ohmline.DeviceGateway.Ocpp.Frames;

namespace Ohmline.DeviceGateway.Tests;

/// <summary>
/// The gateway as <c>Program</c> builds it, in memory, stopping with chargers connected (section
/// 21.7): each charger is sent a clean close with a reconnect hint before the node is gone.
/// A development gateway: a cluster of one, on a free remoting port.
/// </summary>
public sealed class GatewayDrainTests : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _gateway =
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("Akka:Remote:Port", "0")
            .UseSetting("PetabridgeCmd:Port", "0")
            .UseSetting("Ohmline:Gateway:DrainWindow", "00:00:01")
            .UseSetting("Ohmline:Gateway:DrainBatchSize", "2"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_connected_chargers_when_the_gateway_stops_then_each_is_told_to_return()
    {
        var chargers = await Task.WhenAll(
            ConnectAsync("OHM-SLC-0041"),
            ConnectAsync("OHM-SLC-0042"),
            ConnectAsync("OHM-SLC-0043"));
        (await RecordedAsync(chargers.Length)).ShouldBe(chargers.Length);

        var stopping = _gateway.DisposeAsync().AsTask();
        var closes = await Task.WhenAll(chargers.Select(CloseFrameAsync));
        await stopping.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        closes.ShouldAllBe(close => close.Status == ReconnectHint.ServiceRestart);
        closes.Select(close => ReconnectHint.Of(close.Status, close.Description))
            .ShouldAllBe(wait => wait >= TimeSpan.FromSeconds(5)
                                 && wait <= TimeSpan.FromSeconds(30));
    }

    public async ValueTask DisposeAsync() => await _gateway.DisposeAsync();

    private async Task<WebSocket> ConnectAsync(string chargePointId)
    {
        var client = _gateway.Server.CreateWebSocketClient();
        client.SubProtocols.Add(ChargerSockets.Subprotocol);
        return await client.ConnectAsync(
            new Uri(_gateway.Server.BaseAddress, $"ocpp/{chargePointId}"), Ct);
    }

    /// <summary>
    /// How many connections the gateway has recorded, once it has <paramref name="expected"/>
    /// or after ten seconds. A charger's handshake completes before the gateway records its
    /// connection, and a drain closes only the connections it knows of.
    /// </summary>
    private async Task<int> RecordedAsync(int expected)
    {
        var open = _gateway.Services.GetRequiredService<OpenConnections>();
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (open.Current.Count < expected && !patience.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), Ct);
        }

        return open.Current.Count;
    }

    /// <summary>Waits for the gateway's close frame and answers it, as a charger would.</summary>
    private static async Task<(WebSocketCloseStatus? Status, string? Description)> CloseFrameAsync(
        WebSocket charger)
    {
        var buffer = new byte[1024];
        WebSocketReceiveResult received;
        do
        {
            received = await charger.ReceiveAsync(buffer, Ct);
        }
        while (received.MessageType is not WebSocketMessageType.Close);

        await charger.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye", Ct);
        return (received.CloseStatus, received.CloseStatusDescription);
    }
}
