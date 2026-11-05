using System.Collections.Concurrent;
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.Query;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// The runner between the journal and a projection (section 13.4): three <c>charging</c>
/// events at offsets 1, 2 and 3 in a SQLite journal, a projection that writes down each
/// delivery, and offsets in PostgreSQL.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class ProjectionRunnerTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_stored_offset_when_the_runner_starts_then_it_resumes_after_it()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var offsets = new NpgsqlOffsetStore(database);
        await using (var connection = await database.OpenConnectionAsync(Ct))
        await using (var transaction = await connection.BeginTransactionAsync(Ct))
        {
            await offsets.SaveAsync(
                DeliveryLog.Stream, 1, DateTimeOffset.UtcNow, transaction, Ct);
            await transaction.CommitAsync(Ct);
        }

        await RunUntilOffsetAsync(node, database, offsets, new DeliveryLog(), offset: 3);

        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([2, 3]);
    }

    [Fact]
    public async Task Given_a_crash_before_the_offset_save_then_the_write_is_undone_with_it()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var offsets = new CrashingOffsetStore(new NpgsqlOffsetStore(database), crashAt: 2);

        await RunUntilOffsetAsync(node, database, offsets, new DeliveryLog(), offset: 3);

        // One transaction (section 13.5): the crash rolled back offset 2's write with its
        // offset, and the restarted stream delivered it again, so it is there once.
        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Given_a_projection_that_fails_once_then_its_write_is_undone_and_retried()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var offsets = new NpgsqlOffsetStore(database);

        await RunUntilOffsetAsync(
            node, database, offsets, new DeliveryLog(failOnceAt: 2), offset: 3);

        // The failed attempt's insert was rolled back with its transaction.
        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Given_a_crash_before_the_commit_then_the_change_is_published_once_after()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var offsets = new CrashingOffsetStore(new NpgsqlOffsetStore(database), crashAt: 2);
        var published = new ConcurrentQueue<LiveUpdate>();

        await ProjectionRun.UntilOffsetAsync(
            node,
            database,
            offsets,
            new DeliveryLog(),
            ChargingTags.Charging,
            offset: 3,
            Ct,
            published.Enqueue);

        // The first try at offset 2 wrote its row, then crashed before the commit: nothing was
        // published for it, so no client heard of a version the read model never showed.
        await WaitUntilAsync(() => published.Count == 3);
        published.Select(change => ((SessionSummaryChanged)change).Version).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Given_a_handled_event_then_its_offset_row_says_when_it_was_journaled()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var last = (await node.System.ReadJournal
                .CurrentEventsByTag(ChargingTags.Charging, Offset.Sequence(2))
                .RunWith(Sink.First<EventEnvelope>(), node.System.Materializer()))
            .Timestamp;

        await RunUntilOffsetAsync(
            node, database, new NpgsqlOffsetStore(database), new DeliveryLog(), offset: 3);

        await using var select = database.CreateCommand(
            "SELECT last_event_at, updated_at FROM read_models.projection_offsets " +
            "WHERE projection_id = 'delivery-log'");
        await using var reader = await select.ExecuteReaderAsync(Ct);
        (await reader.ReadAsync(Ct)).ShouldBeTrue();
        var lastEventAt = reader.GetFieldValue<DateTimeOffset>(0);
        // The journal's timestamp is in ticks; PostgreSQL keeps microseconds.
        lastEventAt.ShouldBe(new DateTimeOffset(last, TimeSpan.Zero), TimeSpan.FromTicks(10));
        reader.GetFieldValue<DateTimeOffset>(1).ShouldBeGreaterThan(lastEventAt);
    }

    private static async Task<JournalNode> WithThreeEventsAsync(TemporaryDatabase journal)
    {
        var node = await JournalNode.StartAsync(journal, new FakeTimeProvider(Morning), Ct);
        (await node.AskAsync<ChargePointRegion, CommandReply>(Register(Charger), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<ChargePointRegion, CommandReply>(Register(NextCharger), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<ChargePointRegion, CommandReply>(Reserve(Charger, 1), Ct))
            .ShouldBe(new CommandAccepted(2));
        return node;
    }

    private async Task<NpgsqlDataSource> ReadModelsAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await DeliveryLog.CreateTableAsync(database, Ct);
        return database;
    }

    /// <summary>A change is published just after its commit, which the run may beat.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), Ct);
        }
    }

    private static Task RunUntilOffsetAsync(
        JournalNode node,
        NpgsqlDataSource database,
        IOffsetStore offsets,
        IProjectionHandler projection,
        long offset) =>
        ProjectionRun.UntilOffsetAsync(
            node, database, offsets, projection, ChargingTags.Charging, offset, Ct);
}
