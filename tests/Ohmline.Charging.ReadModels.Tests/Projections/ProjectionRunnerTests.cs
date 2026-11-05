using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.Query;
using Ohmline.Testing;
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
    private static readonly RestartSettings QuickBackoff =
        RestartSettings.Create(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), 0);

    private static readonly TimeSpan Patience =
        TimeSpan.FromSeconds(15 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_stored_offset_when_the_runner_starts_then_it_resumes_after_it()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var offsets = new NpgsqlOffsetStore(database);
        await offsets.SaveAsync(DeliveryLog.Stream, 1, Ct);

        await RunUntilOffsetAsync(node, database, offsets, new DeliveryLog(), offset: 3);

        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([2, 3]);
    }

    [Fact]
    public async Task Given_a_crash_between_the_projection_write_and_the_offset_save_then_again()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var offsets = new CrashingOffsetStore(new NpgsqlOffsetStore(database), crashAt: 2);

        await RunUntilOffsetAsync(node, database, offsets, new DeliveryLog(), offset: 3);

        // At least once: the write for offset 2 was committed, its offset was not, so the
        // restarted stream delivered offset 2 a second time.
        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([1, 2, 2, 3]);
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
        await ReadModelSchema.EnsureAsync(database, Ct);
        await DeliveryLog.CreateTableAsync(database, Ct);
        return database;
    }

    /// <summary>Runs the projection until its stored offset is <paramref name="offset"/>.</summary>
    private static async Task RunUntilOffsetAsync(
        JournalNode node,
        NpgsqlDataSource database,
        IOffsetStore offsets,
        IProjectionHandler projection,
        long offset)
    {
        var runner = new ProjectionRunner(
            node.System.ReadJournal,
            database,
            offsets,
            projection,
            ChargingTags.Charging,
            QuickBackoff,
            node.System.Log);
        await using var running = runner.Start(node.System.Materializer());

        var stored = new NpgsqlOffsetStore(database);
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (await stored.LoadAsync(DeliveryLog.Stream, Ct) < offset)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"The projection did not reach offset {offset}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }
}
