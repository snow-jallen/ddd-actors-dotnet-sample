using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;

namespace Ohmline.IntegrationTests;

/// <summary>
/// <c>GET /sessions/{id}/events</c> on the node as it runs (section 14.4): a browser's view of
/// a session, as Server-Sent Events pushed by the session history projection through
/// DistributedPubSub, each with the session's version as its id.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class SessionEventsTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_listening_browser_when_the_session_stops_then_it_gets_version_2()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = await StartAsync(http);

        using var events = await ListenAsync(http, session, lastEventId: null);
        await using var stream = await events.Content.ReadAsStreamAsync(Ct);
        await using var items = SseParser.Create(stream).EnumerateAsync(Ct).GetAsyncEnumerator(Ct);
        var first = await NextAsync(items);
        (await http.PostAsync($"/sessions/{session}/stop", content: null, Ct))
            .EnsureSuccessStatusCode();
        var second = await NextAsync(items);

        events.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        first.EventId.ShouldBe("1");
        Read(first).ShouldBe(new Summary(session, 1, "charging", 0m));
        second.EventId.ShouldBe("2");
        Read(second).ShouldBe(new Summary(session, 2, "ended", 0m));
        (await items.MoveNextAsync()).ShouldBeFalse(); // The stream ends with the session.
    }

    [Fact]
    public async Task Given_a_browser_that_has_seen_the_end_when_it_reconnects_then_204()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = await StartAsync(http);
        var stopped = await http.PostAsync($"/sessions/{session}/stop", content: null, Ct);
        (await http.GetAsync(stopped.Headers.Location, Ct)).EnsureSuccessStatusCode();

        using var missed = await ListenAsync(http, session, lastEventId: "1");
        using var seen = await ListenAsync(http, session, lastEventId: "2");

        missed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = new List<SseItem<string>>();
        await foreach (var item in SseParser.Create(await missed.Content.ReadAsStreamAsync(Ct))
            .EnumerateAsync(Ct))
        {
            items.Add(item);
        }

        items.ShouldHaveSingleItem().EventId.ShouldBe("2"); // Only what it had not seen.
        seen.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

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

    private static async Task<HttpResponseMessage> ListenAsync(
        HttpClient http, Guid session, string? lastEventId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/sessions/{session}/events");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (lastEventId is not null)
        {
            request.Headers.Add("Last-Event-ID", lastEventId);
        }

        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Ct);
    }

    private static async Task<SseItem<string>> NextAsync(IAsyncEnumerator<SseItem<string>> items)
    {
        (await items.MoveNextAsync().AsTask().WaitAsync(Patience, Ct)).ShouldBeTrue();
        return items.Current;
    }

    private static Summary? Read(SseItem<string> item) =>
        JsonSerializer.Deserialize<Summary>(item.Data, Web);

    private sealed record Summary(Guid SessionId, long Version, string Status, decimal EnergyKwh);
}
