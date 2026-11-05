using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Journeys;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.Tests.Projections;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Journeys;

/// <summary>
/// The relay in the projection runner (section 15.4), over a session that Charging's own actor
/// wrote to the in-memory journal: its start and its end reach the journey region, and nothing
/// else does. The region is a stand-in that answers as a journey would.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class RelayRunTests(PostgresFixture postgres)
{
    private const string Tag = ChargingTags.ChargingSession;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_session_then_its_start_and_end_are_told_to_its_journey()
    {
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var heard = new ConcurrentQueue<object>();
        var journeys = journal.System.ActorOf(Props.Create(() => new Journeys(heard)));
        ActorRegistry.For(journal.System).Register<ChargingJourneyRegion>(journeys);
        var clock = new FakeTimeProvider(Morning);
        var session = journal.System.ActorOf(ChargingSessionActor.Props(Session, clock));
        await journal.AskAsync<CommandReply>(session, Begin(), Ct);
        await journal.AskAsync<CommandReply>(
            session, new SuspendCharging(Command, Session, new ByEv()), Ct);
        await journal.AskAsync<CommandReply>(session, new StopSession(Command, Session), Ct);
        await using var database = await postgres.CreateDatabaseAsync(Ct);
        await ReadModelSchema.EnsureAsync(database, Ct);

        var relay = new ProjectionRunner(
            journal.ReadJournal,
            database,
            new NpgsqlOffsetStore(database),
            new JourneySessionRelay().RunningIn(journal.System),
            Tag,
            ProjectionRun.QuickBackoff,
            _ => { },
            journal.System.Log);
        await using (relay.Start(journal.System.Materializer()))
        {
            // The in-memory read journal numbers the tag's events from 0: the end is 2.
            await ProjectionRun.StoredAsync(
                database, new ProjectionStream(JourneySessionRelay.Id, Tag), offset: 2, Ct);
        }

        heard.Select(@event => @event.GetType()).ShouldBe(
            [typeof(SessionStarted), typeof(SessionEnded)]);
    }

    /// <summary>The journey region, as far as the relay can tell.</summary>
    private sealed class Journeys : ReceiveActor
    {
        public Journeys(ConcurrentQueue<object> heard) =>
            ReceiveAny(message =>
            {
                heard.Enqueue(message);
                Sender.Tell(new CommandAccepted(Version: heard.Count));
            });
    }
}
