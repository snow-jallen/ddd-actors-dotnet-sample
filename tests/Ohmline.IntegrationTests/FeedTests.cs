using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.IntegrationTests;

/// <summary>
/// The charging feed on the node as it runs (section 16.2): a session ends, the relay writes
/// it to the outbox, and the feed serves it in the Published Language, page by page.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class FeedTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_session_that_ended_then_the_feed_serves_it_as_completed()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = await ChargeAsync(http);

        var (entry, _) = await FeedEntryAsync(http, session);

        entry.Type.ShouldBe("ohmline.charging.session-completed.v1");
        var completed = entry.Event.Deserialize<ChargingIntegrationEvent>(PublishedLanguage.Json)
            .ShouldBeOfType<SessionCompleted>();
        completed.EnergyKwh.ShouldBe(7.35m);
        completed.Price.ShouldBe(new PriceBreakdown(
            "USD", 3.087m, [new PriceLine("energy", 7.35m, "kWh", 0.42m, 3.087m)]));
    }

    [Fact]
    public async Task Given_a_page_already_read_then_its_etag_answers_not_modified()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = await ChargeAsync(http);
        var (entry, _) = await FeedEntryAsync(http, session);
        var page = $"{ChargingFeedPage.Path}?after={entry.Position - 1}&limit=1";
        var first = await http.GetAsync(page, Ct);
        first.Headers.CacheControl!.ToString().ShouldContain("immutable");

        using var again = new HttpRequestMessage(HttpMethod.Get, page);
        again.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var answer = await http.SendAsync(again, Ct);

        answer.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Given_a_reader_at_the_end_then_the_next_link_asks_the_same_question_again()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = await ChargeAsync(http);
        var (entry, _) = await FeedEntryAsync(http, session);

        var end = await LastPageAsync(http, entry.Position);

        end.Entries.ShouldBeEmpty();
        end.Next.ShouldStartWith($"{ChargingFeedPage.Path}?after=");
        (await http.GetFromJsonAsync<ChargingFeedPage>(end.Next, Ct))!.Next.ShouldBe(end.Next);
    }

    private static async Task<Guid> ChargeAsync(HttpClient http)
    {
        var session = Guid.CreateVersion7();
        var connector = $"{Chargers.Next()}/1";
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/sessions", Sessions.Start(session, connector), Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync(
                $"/sessions/{session}/meter-values", Sessions.Reading(1_207_350), Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsync($"/sessions/{session}/stop", content: null, Ct))
            .EnsureSuccessStatusCode();
        return session;
    }

    /// <summary>Reads the feed from the start, page by page, until the session is in it.</summary>
    private static async Task<(ChargingFeedEntry Entry, string Next)> FeedEntryAsync(
        HttpClient http, Guid session)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        var next = $"{ChargingFeedPage.Path}?after=0&limit=10";
        while (true)
        {
            var page = (await http.GetFromJsonAsync<ChargingFeedPage>(next, Ct))!;
            if (page.Entries.FirstOrDefault(entry => SessionOf(entry) == session) is { } found)
            {
                return (found, page.Next);
            }

            if (page.Entries.IsEmpty)
            {
                if (DateTimeOffset.UtcNow > deadline)
                {
                    throw new TimeoutException($"Session {session} is not in the feed.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
            }

            next = page.Next;
        }
    }

    private static async Task<ChargingFeedPage> LastPageAsync(HttpClient http, long from)
    {
        var page = (await http.GetFromJsonAsync<ChargingFeedPage>(
            $"{ChargingFeedPage.Path}?after={from}", Ct))!;
        while (!page.Entries.IsEmpty)
        {
            page = (await http.GetFromJsonAsync<ChargingFeedPage>(page.Next, Ct))!;
        }

        return page;
    }

    private static Guid? SessionOf(ChargingFeedEntry entry) =>
        entry.Event.TryGetProperty("sessionId", out var id) ? id.GetGuid() : null;
}
