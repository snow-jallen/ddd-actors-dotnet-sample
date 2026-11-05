using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Akka.Actor;
using Microsoft.Extensions.DependencyInjection;

namespace Ohmline.IntegrationTests;

/// <summary>
/// Projection lag on the node as it runs (section 14.3): the read journal polls every 100 ms,
/// so a session is in its history well within a second, and the status endpoint measures it.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class ProjectionLagTests(PostgresFixture postgres, ITestOutputHelper output)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_the_node_then_its_read_journal_polls_every_100_ms()
    {
        await using var node = new OhmlineNode(postgres);
        node.CreateClient();

        var config = node.Services.GetRequiredService<ActorSystem>().Settings.Config
            .GetConfig("akka.persistence.query.journal.sql");

        config.GetTimeSpan("refresh-interval").ShouldBe(TimeSpan.FromMilliseconds(100));
        config.GetTimeSpan("journal-sequence-retrieval.query-delay")
            .ShouldBe(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task Given_tuned_polling_then_a_started_session_is_in_its_history_within_1_s()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        await StartAsync(http, Guid.CreateVersion7(), Patience); // The daemons are running now.

        var session = Guid.CreateVersion7();
        var lag = await StartAsync(http, session, Patience);
        var status = await http.GetFromJsonAsync<List<Stream>>("/projections/status", Ct);

        var history = status.ShouldNotBeNull()
            .Single(stream => stream.ProjectionId == "session-history-v1");
        output.WriteLine(
            $"In its history {lag.TotalMilliseconds:0} ms after the 202; measured lag " +
            $"{history.LagMs} ms");
        lag.ShouldBeLessThan(TimeSpan.FromSeconds(1));
        history.LastEventAt.ShouldNotBeNull();
        history.LagMs.ShouldNotBeNull().ShouldBeInRange(0, 1_000);
    }

    /// <summary>
    /// Starts a session and measures how long its history row takes to appear after the 202:
    /// by then the session's first event is in the journal.
    /// </summary>
    private static async Task<TimeSpan> StartAsync(HttpClient http, Guid session, TimeSpan patience)
    {
        (await http.PostAsJsonAsync("/sessions", Sessions.Start(session), Ct))
            .EnsureSuccessStatusCode();
        var clock = Stopwatch.StartNew();
        while ((await http.GetAsync($"/sessions/{session}", Ct)).StatusCode != HttpStatusCode.OK)
        {
            if (clock.Elapsed > patience)
            {
                throw new TimeoutException($"Session {session} never reached its history.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), Ct);
        }

        return clock.Elapsed;
    }

    private sealed record Stream(
        string ProjectionId,
        string Tag,
        long StoredOffset,
        long EventsBehind,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? LastEventAt,
        long? LagMs);
}
