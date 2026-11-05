using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.SharedKernel.Identity;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// The runner over the in-memory read journal (section 14.7): the events come from memory, with
/// no SQLite file and no cluster to start, and only the read model is in PostgreSQL, because
/// the runner's whole point is the one transaction for the rows and the offset.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class InMemoryRunnerTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_stored_offset_then_it_resumes_after_it_and_keeps_up_with_new_events()
    {
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var clock = new FakeTimeProvider(Morning);
        var charger = journal.System.ActorOf(ChargePointActor.Props(Charger, clock));
        var next = journal.System.ActorOf(ChargePointActor.Props(NextCharger, clock));
        await journal.AskAsync<CommandReply>(charger, Register(Charger), Ct);
        await journal.AskAsync<CommandReply>(next, Register(NextCharger), Ct);
        await journal.AskAsync<CommandReply>(charger, Reserve(Charger, 1), Ct);
        await using var database = await ReadModelsAsync();
        var offsets = new NpgsqlOffsetStore(database);
        await StoreAsync(database, offsets, offset: 1);

        await using (var running = Runner(journal, database, offsets).Start(Materializer(journal)))
        {
            await ProjectionRun.StoredAsync(database, DeliveryLog.Stream, offset: 2, Ct);
            await journal.AskAsync<CommandReply>(next, Reserve(NextCharger, 1), Ct);
            await ProjectionRun.StoredAsync(database, DeliveryLog.Stream, offset: 3, Ct);
        }

        // The in-memory read journal numbers the tag's events from 0: the three before the run
        // are 0, 1 and 2, and the reservation made while it ran is 3.
        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([2, 3]);
    }

    private static ProjectionRunner Runner(
        MemoryJournal journal, NpgsqlDataSource database, IOffsetStore offsets) =>
        new(
            journal.ReadJournal,
            database,
            offsets,
            new DeliveryLog(),
            ChargingTags.Charging,
            ProjectionRun.QuickBackoff,
            _ => { },
            journal.System.Log);

    private static IMaterializer Materializer(MemoryJournal journal) =>
        journal.System.Materializer();

    private static async Task StoreAsync(
        NpgsqlDataSource database, IOffsetStore offsets, long offset)
    {
        await using var connection = await database.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await offsets.SaveAsync(DeliveryLog.Stream, offset, DateTimeOffset.UtcNow, transaction, Ct);
        await transaction.CommitAsync(Ct);
    }

    private async Task<NpgsqlDataSource> ReadModelsAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await ReadModelSchema.EnsureAsync(database, Ct);
        await DeliveryLog.CreateTableAsync(database, Ct);
        return database;
    }
}
