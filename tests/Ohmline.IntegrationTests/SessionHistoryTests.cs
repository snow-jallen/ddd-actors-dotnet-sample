using System.Net.Http.Json;
using Npgsql;

namespace Ohmline.IntegrationTests;

/// <summary>
/// The whole pipeline in the node as it runs: commands over HTTP, events in PostgreSQL's
/// journal, the session history projection running as a sharded daemon, and its row in
/// <c>read_models.session_history</c> (section 13.6).
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class SessionHistoryTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_session_started_and_stopped_then_its_history_row_follows_it()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var session = Guid.CreateVersion7();
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/sessions", Sessions.Start(session), Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync(
                $"/sessions/{session}/meter-values", Sessions.Reading(1_207_350), Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsync($"/sessions/{session}/stop", content: null, Ct))
            .EnsureSuccessStatusCode();

        var (status, energyKwh, version) = await HistoryWhenAtAsync(session, version: 3);

        status.ShouldBe("ended");
        energyKwh.ShouldBe(7.35m);
        version.ShouldBe(3);
    }

    private async Task<(string Status, decimal EnergyKwh, long Version)> HistoryWhenAtAsync(
        Guid session, long version)
    {
        await using var database = NpgsqlDataSource.Create(postgres.ConnectionString);
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            try
            {
                await using var select = database.CreateCommand(
                    "SELECT status, energy_kwh, last_sequence_nr " +
                    "FROM read_models.session_history WHERE session_id = @session_id");
                select.Parameters.AddWithValue("session_id", session);
                await using var reader = await select.ExecuteReaderAsync(Ct);
                if (await reader.ReadAsync(Ct) && reader.GetInt64(2) >= version)
                {
                    return (reader.GetString(0), reader.GetDecimal(1), reader.GetInt64(2));
                }
            }
            catch (PostgresException missing) when (missing.SqlState == "42P01")
            {
                // The projection has not created its table yet.
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Session {session} never reached version {version}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }
}
