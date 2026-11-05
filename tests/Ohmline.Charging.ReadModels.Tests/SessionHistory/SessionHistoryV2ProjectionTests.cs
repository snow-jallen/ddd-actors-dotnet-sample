using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.SessionHistory;
using Ohmline.Charging.ReadModels.Tests.Projections;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.SessionHistory;

/// <summary>
/// Rebuilding side by side (section 20.9): the second version of the session history fills its
/// own table from the journal's first event, under its own offset, while the first version's
/// table and offset are left as they were.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class SessionHistoryV2ProjectionTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_history_already_built_then_v2_is_built_beside_it_from_the_start()
    {
        using var journal = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await JournalNode.StartAsync(journal, clock, Ct);
        await using var database = await postgres.CreateDatabaseAsync(Ct);
        await node.AskAsync<ChargingSessionRegion, CommandReply>(Begin(), Ct);
        clock.Advance(TimeSpan.FromMinutes(30));
        await node.AskAsync<ChargingSessionRegion, CommandReply>(
            new RecordMeterValue(Command, Session, 1_211_000, clock.GetUtcNow()), Ct);
        await node.AskAsync<ChargingSessionRegion, CommandReply>(
            new StopSession(Command, Session), Ct);
        await RunAsync(node, database, new SessionHistoryProjection());

        await RunAsync(node, database, new SessionHistoryV2Projection());

        var v2 = (await FindAsync(database)).ShouldNotBeNull();
        v2.SiteId.ShouldBe("slc-depot");
        v2.Session.ShouldBe(await FindV1Async(database));
        (await OffsetsAsync(database)).ShouldBe(
            [("session-history-v1", 3L), ("session-history-v2", 3L)]);
    }

    private static Task RunAsync(
        JournalNode node, NpgsqlDataSource database, IProjectionHandler projection) =>
        ProjectionRun.UntilOffsetAsync(
            node,
            database,
            new NpgsqlOffsetStore(database),
            projection,
            ChargingTags.ChargingSession,
            offset: 3,
            Ct);

    private static async Task<SessionHistoryV2Row?> FindAsync(NpgsqlDataSource database)
    {
        await using var connection = await database.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        return await SessionHistoryV2Table.FindAsync(transaction, Session.Value, Ct);
    }

    private static async Task<SessionHistoryRow?> FindV1Async(NpgsqlDataSource database)
    {
        await using var connection = await database.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        return await SessionHistoryTable.FindAsync(transaction, Session.Value, Ct);
    }

    private static async Task<List<(string ProjectionId, long Offset)>> OffsetsAsync(
        NpgsqlDataSource database)
    {
        await using var select = database.CreateCommand("""
            SELECT projection_id, last_offset FROM read_models.projection_offsets
            WHERE projection_id LIKE 'session-history-%' ORDER BY projection_id
            """);
        await using var reader = await select.ExecuteReaderAsync(Ct);
        var offsets = new List<(string, long)>();
        while (await reader.ReadAsync(Ct))
        {
            offsets.Add((reader.GetString(0), reader.GetInt64(1)));
        }

        return offsets;
    }
}
