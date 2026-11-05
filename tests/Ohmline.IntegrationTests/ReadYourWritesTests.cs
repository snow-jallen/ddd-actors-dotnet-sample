using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Ohmline.IntegrationTests;

/// <summary>
/// Read-your-writes on the node as it runs (section 14.1): a command's 202 points at the session
/// history at least at the version it produced, and that read waits for the projection, or says
/// that it has not caught up.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class ReadYourWritesTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_stop_when_its_location_is_read_then_the_history_shows_the_stop()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = await StartAsync(http);

        var stopped = await http.PostAsync($"/sessions/{session}/stop", content: null, Ct);
        var read = await http.GetAsync(stopped.Headers.Location, Ct);

        stopped.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await stopped.Content.ReadFromJsonAsync<Accepted>(Ct)).ShouldBe(new Accepted(2));
        stopped.Headers.Location.ShouldBe(
            new Uri($"/sessions/{session}?minVersion=2", UriKind.Relative));
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        read.Headers.ETag.ShouldBe(new("\"2\""));
        var row = await read.Content.ReadFromJsonAsync<Session>(Ct);
        row.ShouldNotBeNull().Status.ShouldBe("ended");
        row.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Given_a_paused_projection_when_read_at_the_new_version_then_202_for_now()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = await StartAsync(http);

        HttpResponseMessage behind;
        await using (var pause = await PauseSessionHistoryAsync())
        {
            await http.PostAsync($"/sessions/{session}/stop", content: null, Ct);
            behind = await http.GetAsync($"/sessions/{session}?minVersion=2", Ct);
        }

        var caughtUp = await EventuallyAsync(http, $"/sessions/{session}?minVersion=2");

        behind.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        behind.Headers.RetryAfter.ShouldBe(new(TimeSpan.FromSeconds(1)));
        (await behind.Content.ReadFromJsonAsync<Behind>(Ct)).ShouldBe(new Behind(1, 2));
        (await caughtUp.Content.ReadFromJsonAsync<Session>(Ct)).ShouldNotBeNull()
            .Status.ShouldBe("ended");
    }

    [Fact]
    public async Task Given_a_session_the_history_has_never_seen_then_404_without_min_version()
    {
        await using var node = new OhmlineNode(postgres);

        var read = await node.CreateClient().GetAsync($"/sessions/{Guid.CreateVersion7()}", Ct);

        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Starts a session and waits until its history row is there, at version 1.</summary>
    private static async Task<Guid> StartAsync(HttpClient http)
    {
        var session = Guid.CreateVersion7();
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        var started = await http.PostAsJsonAsync("/sessions", Sessions.Start(session), Ct);
        started.Headers.Location.ShouldBe(
            new Uri($"/sessions/{session}?minVersion=1", UriKind.Relative));
        await EventuallyAsync(http, started.Headers.Location!.ToString());
        return session;
    }

    /// <summary>
    /// Pauses the session history projection: holds a lock on its offset row, so its next
    /// transaction waits at the offset, uncommitted, until the pause is disposed.
    /// </summary>
    private async Task<IAsyncDisposable> PauseSessionHistoryAsync()
    {
        var database = NpgsqlDataSource.Create(postgres.ConnectionString);
        var connection = await database.OpenConnectionAsync(Ct);
        var transaction = await connection.BeginTransactionAsync(Ct);
        await using var hold = new NpgsqlCommand(
            "SELECT last_offset FROM read_models.projection_offsets " +
            "WHERE projection_id = 'session-history-v1' FOR UPDATE",
            connection,
            transaction);
        await hold.ExecuteNonQueryAsync(Ct);
        return new Pause(database, connection, transaction);
    }

    private static async Task<HttpResponseMessage> EventuallyAsync(HttpClient http, string path)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var response = await http.GetAsync(path, Ct);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return response;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"GET {path} never answered 200.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }

    private sealed class Pause(
        NpgsqlDataSource database, NpgsqlConnection connection, NpgsqlTransaction transaction)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
            await database.DisposeAsync();
        }
    }

    private sealed record Session(
        Guid SessionId,
        string TokenUid,
        string ConnectorId,
        string Status,
        decimal EnergyKwh,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        long Version);

    private sealed record Behind(long Version, long MinVersion);
}
