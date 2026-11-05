using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.ReadModels.Policies;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.Tests.Projections;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Policies;

/// <summary>
/// The subscriber in the projection runner, over a session that Charging's own actor wrote to
/// the in-memory journal: the other contexts are told, and the offset moves past the end like
/// any projection's. The other contexts are stand-ins in the actor registry, under the keys of
/// their shard regions.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class SubscriberRunTests(PostgresFixture postgres)
{
    private const string Tag = ChargingTags.ChargingSession;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_an_ended_session_then_the_others_are_told_and_the_offset_moves_past_it()
    {
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var budgets = Stand(journal, new BudgetCommandAccepted(Version: 3));
        var tokens = Stand(journal, new TokenCommandAccepted(Version: 2));
        ActorRegistry.For(journal.System).Register<SitePowerBudgetRegion>(budgets.Actor);
        ActorRegistry.For(journal.System).Register<IdTokenRegion>(tokens.Actor);
        var clock = new FakeTimeProvider(Morning);
        var session = journal.System.ActorOf(ChargingSessionActor.Props(Session, clock));
        await journal.AskAsync<CommandReply>(session, Begin(), Ct);
        await journal.AskAsync<CommandReply>(session, new StopSession(Command, Session), Ct);
        await using var database = await postgres.CreateDatabaseAsync(Ct);
        await ReadModelSchema.EnsureAsync(database, Ct);

        await using (Run(journal, database))
        {
            // The in-memory read journal numbers the tag's events from 0: the end is 1.
            await ProjectionRun.StoredAsync(
                database, new ProjectionStream(SessionEndedSubscriber.Id, Tag), offset: 1, Ct);
        }

        budgets.Heard.ShouldHaveSingleItem().ShouldBeOfType<ReleaseAllocation>()
            .ShouldSatisfyAllConditions(
                release => release.SiteId.ShouldBe(Depot),
                release => release.SessionId.ShouldBe(Session));
        tokens.Heard.ShouldHaveSingleItem().ShouldBeOfType<UnlockFromSession>()
            .ShouldSatisfyAllConditions(
                unlock => unlock.TokenUid.ShouldBe(DriversCard),
                unlock => unlock.SessionId.ShouldBe(Session));
    }

    private static RunningProjection Run(MemoryJournal journal, NpgsqlDataSource database) =>
        new ProjectionRunner(
                journal.ReadJournal,
                database,
                new NpgsqlOffsetStore(database),
                new SessionEndedSubscriber().RunningIn(journal.System),
                Tag,
                ProjectionRun.QuickBackoff,
                _ => { },
                journal.System.Log)
            .Start(journal.System.Materializer());

    private static StandIn Stand(MemoryJournal journal, object reply)
    {
        var heard = new ConcurrentQueue<object>();
        var actor = journal.System.ActorOf(Props.Create(() => new Answering(heard, reply)));
        return new StandIn(actor, heard);
    }

    private sealed record StandIn(IActorRef Actor, ConcurrentQueue<object> Heard);

    /// <summary>Another context's shard region, as far as the subscriber can tell.</summary>
    private sealed class Answering : ReceiveActor
    {
        public Answering(ConcurrentQueue<object> heard, object reply) =>
            ReceiveAny(message =>
            {
                heard.Enqueue(message);
                Sender.Tell(reply);
            });
    }
}
