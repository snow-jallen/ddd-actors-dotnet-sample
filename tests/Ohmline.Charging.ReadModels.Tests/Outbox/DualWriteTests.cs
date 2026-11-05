using System.Collections.Concurrent;
using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Integration;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.ReadModels.Outbox;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.Tests.Projections;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Outbox;

/// <summary>
/// On branch <c>wrong/ch16-s01-dual-write</c> only: the relay publishes to a broker and then
/// lets the runner store its offset, two writes to two systems with no transaction around them.
/// The process dies between them once.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class DualWriteTests(PostgresFixture postgres)
{
    private const string Tag = ChargingTags.ChargingSession;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Demonstrates_a_crash_after_publishing_publishes_the_event_twice()
    {
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var clock = new FakeTimeProvider(Morning);
        var session = journal.System.ActorOf(ChargingSessionActor.Props(Session, clock));
        await journal.AskAsync<CommandReply>(session, Begin(), Ct);
        await journal.AskAsync<CommandReply>(session, new StopSession(Command, Session), Ct);
        await using var database = await postgres.CreateDatabaseAsync(Ct);
        await ReadModelSchema.EnsureAsync(database, Ct);
        var broker = new ConcurrentQueue<ChargingIntegrationEvent>();

        var runner = new ProjectionRunner(
            journal.ReadJournal,
            database,
            // The crash: the end (offset 1) is published, and its offset is not stored.
            new CrashingOffsetStore(new NpgsqlOffsetStore(database), crashAt: 1),
            new IntegrationEventRelay(@event =>
            {
                broker.Enqueue(@event);
                return Task.CompletedTask;
            }),
            Tag,
            ProjectionRun.QuickBackoff,
            _ => { },
            journal.System.Log);
        await using (runner.Start(journal.System.Materializer()))
        {
            await ProjectionRun.StoredAsync(
                database, new ProjectionStream(IntegrationEventRelay.Id, Tag), offset: 1, Ct);
        }

        // Billing is told twice about one session. Publish after the commit instead, and the
        // same crash tells Billing nothing at all.
        broker.Count.ShouldBe(2);
        broker.Distinct().ShouldHaveSingleItem();
    }
}
