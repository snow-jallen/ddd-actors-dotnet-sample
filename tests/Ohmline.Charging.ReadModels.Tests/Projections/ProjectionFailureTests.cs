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
/// A projection that cannot handle one event, at offset 2 of 3, under each failure policy, and
/// a lost connection that must not count against the event (section 13.7).
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class ProjectionFailureTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience =
        TimeSpan.FromSeconds(15 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_stop_after_retries_when_an_event_always_fails_then_it_stops_before_it()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var projection = new DeliveryLog(failAlwaysAt: 2, policy: new StopAfterRetries(2));

        await using var running = Runner(node, database, projection).Start(Materializer(node));
        var stopped = await Should.ThrowAsync<ProjectionStoppedException>(
            running.Completion.WaitAsync(Patience, Ct));

        stopped.Offset.ShouldBe(2);
        projection.AttemptsAt(2).ShouldBe(3); // The first attempt and two retries.
        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([1]);
        (await new NpgsqlOffsetStore(database).LoadAsync(DeliveryLog.Stream, Ct)).ShouldBe(1);
        (await FailuresAsync(database)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_stopped_projection_when_fixed_and_restarted_then_nothing_is_lost()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var broken = new DeliveryLog(failAlwaysAt: 2, policy: new StopAfterRetries(0));
        await using (var running = Runner(node, database, broken).Start(Materializer(node)))
        {
            await Should.ThrowAsync<ProjectionStoppedException>(
                running.Completion.WaitAsync(Patience, Ct));
        }

        await ProjectionRun.UntilOffsetAsync(
            node,
            database,
            new NpgsqlOffsetStore(database),
            new DeliveryLog(),
            ChargingTags.Charging,
            offset: 3,
            Ct);

        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Given_record_and_skip_when_an_event_always_fails_then_the_rest_is_handled()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var projection = new DeliveryLog(failAlwaysAt: 2, policy: new RecordAndSkip(2));

        await ProjectionRun.UntilOffsetAsync(
            node,
            database,
            new NpgsqlOffsetStore(database),
            projection,
            ChargingTags.Charging,
            offset: 3,
            Ct);

        projection.AttemptsAt(2).ShouldBe(3);
        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([1, 3]);
        var failure = (await FailuresAsync(database)).ShouldHaveSingleItem();
        failure.ProjectionId.ShouldBe("delivery-log");
        failure.PersistenceId.ShouldBe("charge-point-OHM-SLC-0043");
        failure.SequenceNr.ShouldBe(1);
        failure.Error.ShouldContain("Failed on purpose at offset 2.");
    }

    [Fact]
    public async Task Given_a_lost_connection_more_often_than_the_retries_then_it_goes_through()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await WithThreeEventsAsync(journal);
        await using var database = await ReadModelsAsync();
        var projection = new DeliveryLog(
            connectionLostAt: (Offset: 2, Times: 3), policy: new StopAfterRetries(1));

        await ProjectionRun.UntilOffsetAsync(
            node,
            database,
            new NpgsqlOffsetStore(database),
            projection,
            ChargingTags.Charging,
            offset: 3,
            Ct);

        projection.AttemptsAt(2).ShouldBe(4); // Three lost connections, then it went through.
        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([1, 2, 3]);
    }

    private static ProjectionRunner Runner(
        JournalNode node, NpgsqlDataSource database, IProjectionHandler projection) =>
        new(
            node.System.ReadJournal,
            database,
            new NpgsqlOffsetStore(database),
            projection,
            ChargingTags.Charging,
            ProjectionRun.QuickBackoff,
            _ => { },
            node.System.Log);

    private static IMaterializer Materializer(JournalNode node) => node.System.Materializer();

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

    private static async Task<IReadOnlyList<Failure>> FailuresAsync(NpgsqlDataSource database)
    {
        await using var select = database.CreateCommand(
            "SELECT projection_id, persistence_id, sequence_nr, error " +
            "FROM read_models.projection_failures");
        await using var reader = await select.ExecuteReaderAsync(Ct);
        var failures = new List<Failure>();
        while (await reader.ReadAsync(Ct))
        {
            failures.Add(new Failure(
                reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3)));
        }

        return failures;
    }

    private sealed record Failure(
        string ProjectionId, string PersistenceId, long SequenceNr, string Error);
}
