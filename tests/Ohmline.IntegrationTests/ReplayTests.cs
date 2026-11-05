using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Ohmline.IntegrationTests;

/// <summary>
/// Replaying the journal through the projections again (section 14.7): with every offset set
/// back to 0, a new node hands each projection every event a second time, and the rows of the
/// session history, the site live board and the driver app's cards come out the same.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class ReplayTests(PostgresFixture postgres)
{
    private const string Projections =
        "projection_id IN ('session-history-v1', 'site-live-board-v1', 'driver-app-v1')";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_every_event_replayed_from_the_start_then_the_same_rows()
    {
        var (chargePoint, session) = (Chargers.Next(), Guid.CreateVersion7());
        await using var database = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var node = new OhmlineNode(postgres))
        {
            await ChargeAndStopAsync(node.CreateClient(), chargePoint, session);
            // Each read model is a stream of its own, and on a node that has just started they
            // may all still be starting: longer than read-your-writes waits.
            await EventuallyAsync(database, $"""
                SELECT count(*) FROM read_models.session_history
                WHERE session_id = '{session}' AND status = 'ended'
                """);
            await EventuallyAsync(database, $"""
                SELECT count(*) FROM read_models.driver_app_cards
                WHERE session_id = '{session}' AND version = 3
                """);
            await EventuallyAsync(database, $"""
                SELECT count(*) FROM read_models.site_live_board
                WHERE connector_id = '{chargePoint}/1' AND status = 'occupied'
                """);
        }

        var before = await RowsAsync(database, chargePoint, session);
        var reached = await OffsetsAsync(database);
        await ExecuteAsync(database, $"""
            UPDATE read_models.projection_offsets SET last_offset = 0 WHERE {Projections}
            """);

        await using (var replaying = new OhmlineNode(postgres))
        {
            replaying.CreateClient();
            foreach (var (projectionId, tag, offset) in reached)
            {
                await EventuallyAsync(database, $"""
                    SELECT count(*) FROM read_models.projection_offsets
                    WHERE projection_id = '{projectionId}' AND tag = '{tag}'
                      AND last_offset >= {offset}
                    """);
            }
        }

        var after = await RowsAsync(database, chargePoint, session);
        after.ShouldBe(before);
        reached.ShouldContain(stream => stream.ProjectionId == "driver-app-v1");
    }

    /// <summary>
    /// A listed charge point with a car plugged in, and a session on it that is metered and
    /// stopped: events for all three read models.
    /// </summary>
    private static async Task ChargeAndStopAsync(HttpClient http, string chargePoint, Guid session)
    {
        const string token = "04A2B3C4D5E6F7";
        (await http.PostAsJsonAsync("/charge-points", Chargers.Registration(chargePoint), Ct))
            .EnsureSuccessStatusCode();
        var listing =
            new { displayName = "Depot", accessInstructions = "Gate", openingHours = "24/7" };
        (await http.PutAsJsonAsync($"/charge-points/{chargePoint}/listing", listing, Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync(
                $"/dev/charge-points/{chargePoint}/connectors/1/plug-in",
                new { sessionId = session, tokenUid = token },
                Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync(
                "/sessions", Sessions.Start(session, $"{chargePoint}/1", token), Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync(
                $"/sessions/{session}/meter-values", Sessions.Reading(1_207_350), Ct))
            .EnsureSuccessStatusCode();
        var stopped = await http.PostAsync($"/sessions/{session}/stop", content: null, Ct);
        stopped.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>The test's rows in all three read models, as JSON, and its feed's length.</summary>
    private static async Task<string[]> RowsAsync(
        NpgsqlDataSource database, string chargePoint, Guid session) =>
    [
        await JsonAsync(database, $"""
            SELECT row_to_json(h)::text FROM read_models.session_history h
            WHERE session_id = '{session}'
            """),
        await JsonAsync(database, $"""
            SELECT json_agg(b ORDER BY connector_id)::text FROM read_models.site_live_board b
            WHERE connector_id LIKE '{chargePoint}/%'
            """),
        await JsonAsync(database, $"""
            SELECT row_to_json(c)::text FROM read_models.driver_app_cards c
            WHERE session_id = '{session}'
            """),
        await JsonAsync(database, $"""
            SELECT count(*)::text FROM read_models.driver_app_feed WHERE session_id = '{session}'
            """),
    ];

    private static async Task<List<(string ProjectionId, string Tag, long Offset)>> OffsetsAsync(
        NpgsqlDataSource database)
    {
        await using var select = database.CreateCommand(
            $"SELECT projection_id, tag, last_offset FROM read_models.projection_offsets " +
            $"WHERE {Projections}");
        await using var reader = await select.ExecuteReaderAsync(Ct);
        var offsets = new List<(string, string, long)>();
        while (await reader.ReadAsync(Ct))
        {
            offsets.Add((reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        }

        return offsets;
    }

    private static async Task<string> JsonAsync(NpgsqlDataSource database, string sql)
    {
        await using var select = database.CreateCommand(sql);
        return (string)(await select.ExecuteScalarAsync(Ct))!;
    }

    private static async Task ExecuteAsync(NpgsqlDataSource database, string sql)
    {
        await using var command = database.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>Waits until the count query finds a row.</summary>
    private static async Task EventuallyAsync(NpgsqlDataSource database, string count)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            try
            {
                await using var select = database.CreateCommand(count);
                if ((long)(await select.ExecuteScalarAsync(Ct))! > 0)
                {
                    return;
                }
            }
            catch (PostgresException missing) when (missing.SqlState == "42P01")
            {
                // The projections have not created their tables yet.
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Never true: {count}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }
}
