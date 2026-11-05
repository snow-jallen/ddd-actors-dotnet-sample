using System.Net.Http.Json;
using Npgsql;

namespace Ohmline.IntegrationTests;

/// <summary>
/// <c>GET /projections/status</c> on the node as it runs (section 13.7): each stream's stored
/// offset beside the journal, and how many events of its tag it has not handled yet.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class ProjectionStatusTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_running_projection_then_it_is_listed_and_catches_up()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        await StartSessionAsync(http);

        var stream = await EventuallyAsync(
            http, status => status.SingleOrDefault(row =>
                row is { ProjectionId: "session-history-v1", EventsBehind: 0, StoredOffset: > 0 }));

        stream.Tag.ShouldBe("charging-session");
        stream.JournalMaxOrdering.ShouldBeGreaterThanOrEqualTo(stream.StoredOffset);
        stream.Failures.ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_stream_stuck_at_its_start_then_every_event_of_its_tag_is_behind()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        await StartSessionAsync(http);
        await EventuallyAsync(http, status => status.FirstOrDefault()); // Tables exist now.
        await using var database = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var stuck = database.CreateCommand(
            "INSERT INTO read_models.projection_offsets (projection_id, tag, last_offset) " +
            "VALUES ('stuck-v1', 'charging-session', 0) ON CONFLICT DO NOTHING"))
        {
            await stuck.ExecuteNonQueryAsync(Ct);
        }

        await using var count = database.CreateCommand(
            "SELECT count(*) FROM tags WHERE tag = 'charging-session'");
        var sessionEvents = (long)(await count.ExecuteScalarAsync(Ct))!;

        var stream = await EventuallyAsync(
            http, status => status.SingleOrDefault(row => row.ProjectionId == "stuck-v1"));
        stream.StoredOffset.ShouldBe(0);
        stream.EventsBehind.ShouldBe(sessionEvents);
        stream.EventsBehind.ShouldBeGreaterThan(0);
    }

    private static async Task StartSessionAsync(HttpClient http)
    {
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/sessions", Sessions.Start(Guid.CreateVersion7()), Ct))
            .EnsureSuccessStatusCode();
    }

    /// <summary>Asks for the status until <paramref name="pick"/> finds what it wants.</summary>
    private static async Task<Stream> EventuallyAsync(
        HttpClient http, Func<IReadOnlyList<Stream>, Stream?> pick)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var status = await http.GetFromJsonAsync<List<Stream>>("/projections/status", Ct);
            if (pick(status.ShouldNotBeNull()) is { } stream)
            {
                return stream;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The projection status never showed it.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }

    private sealed record Stream(
        string ProjectionId,
        string Tag,
        long StoredOffset,
        long JournalMaxOrdering,
        long EventsBehind,
        long Failures,
        DateTimeOffset UpdatedAt);
}
