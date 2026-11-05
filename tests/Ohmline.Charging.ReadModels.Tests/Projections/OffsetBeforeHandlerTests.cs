using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.Query;
using Ohmline.Testing;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// On branch <c>wrong/ch13-s04-offset-before-handler</c> only: the runner stores the offset
/// first, so the same crash as on main, between the two writes, now falls after the offset and
/// before the projection's write.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class OffsetBeforeHandlerTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience =
        TimeSpan.FromSeconds(15 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Demonstrates_a_crash_after_the_offset_save_loses_the_event_for_good()
    {
        using var journal = new TemporaryDatabase();
        await using var node = await JournalNode.StartAsync(
            journal, new FakeTimeProvider(Morning), Ct);
        (await node.AskAsync<ChargePointRegion, CommandReply>(Register(Charger), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<ChargePointRegion, CommandReply>(Register(NextCharger), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<ChargePointRegion, CommandReply>(Reserve(Charger, 1), Ct))
            .ShouldBe(new CommandAccepted(2));
        await using var database = await postgres.CreateDatabaseAsync(Ct);
        await ReadModelSchema.EnsureAsync(database, Ct);
        await DeliveryLog.CreateTableAsync(database, Ct);
        var offsets = new NpgsqlOffsetStore(database);

        var runner = new ProjectionRunner(
            node.System.ReadJournal,
            database,
            offsets,
            new DeliveryLog(failOnceAt: 2), // The crash: offset 2 is stored, its write is not.
            ChargingTags.Charging,
            RestartSettings.Create(
                TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), 0),
            node.System.Log);
        await using (runner.Start(node.System.Materializer()))
        {
            var deadline = DateTimeOffset.UtcNow + Patience;
            while ((await DeliveryLog.OffsetsAsync(database, Ct)).Count < 2
                   && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
            }
        }

        // At most once: the restart resumed after the stored offset 2, so the event at offset
        // 2, OHM-SLC-0043's registration, never reached the read model.
        (await DeliveryLog.OffsetsAsync(database, Ct)).ShouldBe([1, 3]);
        (await offsets.LoadAsync(DeliveryLog.Stream, Ct)).ShouldBe(3);
    }
}
