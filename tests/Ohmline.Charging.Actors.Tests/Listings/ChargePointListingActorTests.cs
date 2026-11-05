using Akka.Actor;
using Akka.Persistence;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Domain.Listings;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Listings;

/// <summary>
/// The listing in the shell: revisions are accepted and counted, and the journal keeps only the
/// latest snapshot, so a new incarnation recovers from it alone.
/// </summary>
public sealed class ChargePointListingActorTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(ChargePointListingActorTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string PersistenceId => ChargePointListingActor.PersistenceIdOf(Charger);

    [Fact]
    public async Task Given_a_charge_point_never_listed_then_its_view_has_revision_0()
    {
        var listing = Start();

        listing.Tell(new GetChargePointListing(Charger), TestActor);

        await ExpectMsgAsync(new ChargePointListingView(Charger, 0), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_revisions_then_each_one_increments_the_revision()
    {
        var listing = Start();

        await AcceptAsync(listing, Revise("Depot, public bays", "Level -1"), version: 1);
        await AcceptAsync(listing, Revise("Depot, public bays", "Level -2, bays 14-19"), 2);

        listing.Tell(new GetChargePointListing(Charger), TestActor);
        await ExpectMsgAsync(
            new ChargePointListingView(
                Charger, 2, "Depot, public bays", "Level -2, bays 14-19", "24/7"),
            cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_blank_display_name_then_the_revision_is_refused()
    {
        var listing = Start();

        listing.Tell(Revise(" ", "Level -1"), TestActor);

        (await ExpectMsgAsync<CommandRejected>(cancellationToken: Ct))
            .Code.ShouldBe("display-name-missing");
    }

    [Fact]
    public async Task Given_revisions_when_a_new_incarnation_starts_then_the_snapshot_is_enough()
    {
        var first = Start();
        await AcceptAsync(first, Revise("Depot", "Level -1"), version: 1);
        await SettledAsync(version: 1);
        await AcceptAsync(first, Revise("Depot", "Level -2"), version: 2);
        await SettledAsync(version: 2);
        await AcceptAsync(first, Revise("Depot, public bays", "Level -2, bays 14-19"), 3);
        await SettledAsync(version: 3);
        await WatchAsync(first);
        Sys.Stop(first);
        await ExpectTerminatedAsync(first, cancellationToken: Ct);

        IActorRef second = ActorRefs.Nobody;
        await EventFilter.Info(contains: ": snapshot at 3, 0 events replayed").ExpectOneAsync(() =>
        {
            second = Start();
            return Task.CompletedTask;
        }, Ct);

        second.Tell(new GetChargePointListing(Charger), TestActor);
        (await ExpectMsgAsync<ChargePointListingView>(cancellationToken: Ct))
            .ShouldBe(new ChargePointListingView(
                Charger, 3, "Depot, public bays", "Level -2, bays 14-19", "24/7"));
        await AcceptAsync(second, Revise("Depot, public bays", "Level -3"), version: 4);
    }

    /// <summary>
    /// Waits until the revision's snapshot round is over: its events deleted and the snapshots
    /// before it gone. The test kit's in-memory snapshot store can lose a snapshot that is saved
    /// while an older one is being deleted (errata, Chapter 13), so the test lets each round
    /// finish before it sends the next revision.
    /// </summary>
    private Task SettledAsync(long version) =>
        AwaitAssertAsync(
            async () =>
            {
                (await JournalProbe.EventsAsync<ChargePointListingEvent>(Sys, PersistenceId, Ct))
                    .ShouldBeEmpty();
                var store = Akka.Persistence.Persistence.Instance.Apply(Sys).SnapshotStoreFor(null);
                var before = new SnapshotSelectionCriteria(maxSequenceNr: version - 1);
                var older = await store.Ask<LoadSnapshotResult>(
                    new LoadSnapshot(PersistenceId, before, long.MaxValue),
                    TimeSpan.FromSeconds(3),
                    Ct);
                older.Snapshot.ShouldBeNull();
            },
            cancellationToken: Ct);

    private IActorRef Start() => Sys.ActorOf(ChargePointListingActor.Props(Charger, _clock));

    private static ReviseChargePointListing Revise(string name, string access) =>
        new(Command, Charger, name, access, "24/7");

    private async Task AcceptAsync(
        IActorRef listing, ChargePointListingCommand command, long version)
    {
        listing.Tell(command, TestActor);
        await ExpectMsgAsync(new CommandAccepted(version), cancellationToken: Ct);
    }
}
