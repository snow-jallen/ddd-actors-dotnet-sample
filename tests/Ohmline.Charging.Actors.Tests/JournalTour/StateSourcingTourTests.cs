using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Listings;
using Ohmline.Charging.Actors.Tests.Sqlite;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.JournalTour;

/// <summary>
/// What a state-sourced aggregate leaves in SQLite (section 11.7): after three revisions of
/// charge point OHM-SLC-0042's listing, one snapshot and one journal row marked deleted. The rows
/// are written to the test output; <c>docs/persistence-choices.md</c> quotes them.
/// </summary>
public sealed class StateSourcingTourTests(ITestOutputHelper output)
{
    /// <summary>
    /// The first answer waits for the node to start on its SQLite file, which takes seconds
    /// when every test assembly runs at once.
    /// </summary>
    private static readonly TimeSpan AskTimeout =
        TimeSpan.FromSeconds(20 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string PersistenceId => ChargePointListingActor.PersistenceIdOf(Charger);

    [Fact]
    public async Task Given_three_revisions_then_a_snapshot_and_one_deleted_row_remain()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await SqliteNode.StartAsync(database, clock, Ct);
        await ReviseThreeTimesAsync(node.System, clock);

        await using var connection = await OpenAsync(database);
        var (rows, snapshots) = await SettledAsync(connection);
        output.WriteLine("journal:");
        rows.ForEach(row => output.WriteLine(row.ToString()));
        output.WriteLine("snapshot:");
        snapshots.ForEach(snapshot => output.WriteLine(snapshot.ToString()));

        // Events 1 and 2 are gone. The last one stays, marked deleted: it is never replayed,
        // and it is how the journal still knows that the next sequence number is 4.
        var last = rows.ShouldHaveSingleItem();
        last.SequenceNumber.ShouldBe(3);
        last.Deleted.ShouldBeTrue();
        last.Manifest.ShouldBe("charging.charge-point-listing-revised.v1");
        var snapshot = snapshots.ShouldHaveSingleItem();
        snapshot.SequenceNumber.ShouldBe(3);
        snapshot.Manifest.ShouldBe("charging.charge-point-listing-snapshot.v1");
        (await JournalTourQueries.ReadLinesAsync(
                connection, JournalTourQueries.Schema, PersistenceId, Ct))
            .ShouldNotContain(line => line.Contains("journal_metadata"));
    }

    [Fact]
    public async Task Given_a_listing_when_the_node_restarts_then_its_snapshot_is_enough()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using (var node = await SqliteNode.StartAsync(database, clock, Ct))
        {
            await ReviseThreeTimesAsync(node.System, clock);
            await using var connection = await OpenAsync(database);
            await SettledAsync(connection);
        }

        await using var restarted = await SqliteNode.StartAsync(database, clock, Ct);
        var logs = new TestProbe(restarted.System, new XunitAssertions());
        restarted.System.EventStream.Subscribe(logs.Ref, typeof(Info));
        var listing = restarted.System.ActorOf(ChargePointListingActor.Props(Charger, clock));

        var view = await listing.Ask<ChargePointListingView>(
            new GetChargePointListing(Charger), AskTimeout, Ct);
        view.Revision.ShouldBe(3);
        view.AccessInstructions.ShouldBe("Level -2, bays 14-19");
        await logs.FishForMessageAsync<Info>(
            info => $"{info.Message}".Contains(
                $"{PersistenceId} recovered to sequence number 3 in ") &&
                $"{info.Message}".EndsWith(": snapshot at 3, 0 events replayed"),
            cancellationToken: Ct);
        (await listing.Ask<CommandReply>(Revise("Level -3"), AskTimeout, Ct))
            .ShouldBe(new CommandAccepted(4)); // The deleted row kept the sequence number.
    }

    private static async Task ReviseThreeTimesAsync(ActorSystem system, TimeProvider clock)
    {
        var listing = system.ActorOf(ChargePointListingActor.Props(Charger, clock));
        var version = 0L;
        foreach (var access in new[] { "Level -1", "Level -2", "Level -2, bays 14-19" })
        {
            (await listing.Ask<CommandReply>(Revise(access), AskTimeout, Ct))
                .ShouldBe(new CommandAccepted(++version));
        }
    }

    private static ReviseChargePointListing Revise(string access) =>
        new(Command, Charger, "Salt Lake bus depot, public bays", access, "24/7");

    /// <summary>
    /// Snapshots are saved, and events and older snapshots deleted, in the background: read the
    /// tables until they hold one row each, or give up and let the assertions say why.
    /// </summary>
    private static async Task<(List<JournalRow> Rows, List<SnapshotRow> Snapshots)> SettledAsync(
        SqliteConnection connection)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5 * ActorTestConfig.TimeFactor);
        while (true)
        {
            var rows = await JournalTourQueries.ReadJournalAsync(connection, PersistenceId, Ct);
            var snapshots =
                await JournalTourQueries.ReadSnapshotsAsync(connection, PersistenceId, Ct);
            if (rows is [{ Deleted: true }] && snapshots is [_]
                || DateTimeOffset.UtcNow > deadline)
            {
                return ([.. rows], [.. snapshots]);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    private static async Task<SqliteConnection> OpenAsync(TemporaryDatabase database)
    {
        var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        return connection;
    }
}
