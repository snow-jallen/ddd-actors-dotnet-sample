using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Ohmline.IntegrationTests;

/// <summary>
/// The SignalR hub on the node as it runs (section 14.5): an app client joins a session's
/// group and hears the projection's commits, carried to the hub by DistributedPubSub and the
/// node's <c>LiveUpdatesBridge</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class LiveHubTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_client_in_a_sessions_group_when_it_stops_then_it_hears_version_2()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = await StartAsync(http);
        await using var hub = Connect(node);
        var summaries = Channel.CreateUnbounded<Summary>();
        hub.On<Summary>("sessionSummaryChanged", summary => summaries.Writer.TryWrite(summary));
        await hub.StartAsync(Ct);
        await hub.InvokeAsync("JoinSession", session, Ct);

        (await http.PostAsync($"/sessions/{session}/stop", content: null, Ct))
            .EnsureSuccessStatusCode();
        var heard = await summaries.Reader.ReadAsync(Ct).AsTask().WaitAsync(Patience, Ct);

        heard.ShouldBe(new Summary(session, 2, "ended", 0m));
    }

    [Fact]
    public async Task Given_a_token_uid_that_is_not_one_then_joining_its_group_fails()
    {
        await using var node = new OhmlineNode(postgres);
        await using var hub = Connect(node);
        await hub.StartAsync(Ct);

        var failure = await Should.ThrowAsync<HubException>(
            () => hub.InvokeAsync("JoinToken", "not-a-card", Ct));

        failure.Message.ShouldContain("not-a-card");
    }

    /// <summary>
    /// A hub connection through the test server's handler. The test server speaks HTTP only,
    /// so the connection long-polls instead of opening a WebSocket.
    /// </summary>
    private static HubConnection Connect(OhmlineNode node) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(node.Server.BaseAddress, "/hubs/charging"), options =>
            {
                options.HttpMessageHandlerFactory = _ => node.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    private static async Task<Guid> StartAsync(HttpClient http)
    {
        var session = Guid.CreateVersion7();
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        var started = await http.PostAsJsonAsync("/sessions", Sessions.Start(session), Ct);
        var deadline = DateTimeOffset.UtcNow + Patience;
        while ((await http.GetAsync(started.Headers.Location, Ct)).StatusCode != HttpStatusCode.OK)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Session {session} never reached its history.");
            }
        }

        return session;
    }

    private sealed record Summary(Guid SessionId, long Version, string Status, decimal EnergyKwh);
}
