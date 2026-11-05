using Akka.Event;
using Akka.Hosting;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Tests.JournalTour;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// Snapshots are a cache (section 20.6). A snapshot this code cannot read, here one in a version
/// it has never heard of, as a newer node would leave behind after a rollback, is skipped: the
/// charge point replays all its events and saves a snapshot it can read. A state-sourced listing,
/// whose events before its snapshot are deleted, refuses to start empty instead.
/// </summary>
public sealed class UnreadableSnapshotTests
{
    private const string NewerVersion = "charging.charge-point-snapshot.v9";

    private static readonly TimeSpan Patience =
        TimeSpan.FromSeconds(5 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_snapshot_in_an_unknown_version_then_it_is_skipped_and_replaced()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using (var node = await SqliteNode.StartAsync(database, clock, Ct))
        {
            await node.AskAsync<CommandReply>(Register(), Ct);
            for (var version = 2; version <= 100; version++)
            {
                await node.AskAsync<CommandReply>(
                    version % 2 == 0 ? Reserve(1, DriversCard) : Cancel(1, DriversCard), Ct);
            }

            (await SnapshotAsync(database, "charging.charge-point-snapshot.v2")).ShouldBe(100);
        }

        await ExecuteAsync(database, $"UPDATE snapshot SET manifest = '{NewerVersion}'");

        await using var restarted = await SqliteNode.StartAsync(database, clock, Ct);
        var status = await restarted.AskAsync<ChargePointStatus>(Status(), Ct);

        status.Version.ShouldBe(100);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Reserved);
        (await SnapshotAsync(database, "charging.charge-point-snapshot.v2")).ShouldBe(100);
    }

    [Fact]
    public async Task Given_a_state_sourced_listing_whose_snapshot_is_unreadable_then_it_stops()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using (var node = await SqliteNode.StartAsync(database, clock, Ct))
        {
            await node.AskAsync<ChargePointListingRegion, CommandReply>(Revise("Bus depot"), Ct);
            await node.AskAsync<ChargePointListingRegion, CommandReply>(Revise("Depot"), Ct);
            await EventsDeletedBeforeAsync(database, 2);
        }

        await ExecuteAsync(
            database,
            "UPDATE snapshot SET manifest = 'charging.charge-point-listing-snapshot.v9'");

        await using var restarted = await SqliteNode.StartAsync(database, clock, Ct);
        var errors = new TestProbe(restarted.System, new XunitAssertions());
        restarted.System.EventStream.Subscribe(errors.Ref, typeof(Error));
        var listing = await ActorRegistry.For(restarted.System)
            .GetAsync<ChargePointListingRegion>(Ct);
        listing.Tell(new GetChargePointListing(Charger), errors.Ref);

        var error = await errors.ExpectMsgAsync<Error>(Patience, cancellationToken: Ct);
        error.Message.ToString().ShouldBe(
            "charge-point-listing-OHM-SLC-0042 cannot recover: no snapshot was read, and its " +
            "events start at sequence number 3, not 1. It stops rather than run on part of its " +
            "history; see docs/runbooks/poison-event.md");
    }

    private static ReviseChargePointListing Revise(string displayName) => new(
        Command, Charger, displayName, "Level -2, bays 40 to 48", "24/7");

    /// <summary>The newest snapshot with the manifest, once the background save is done.</summary>
    private static async Task<long?> SnapshotAsync(TemporaryDatabase database, string manifest)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var rows = await JournalTourQueries.ReadSnapshotsAsync(
                connection, "charge-point-OHM-SLC-0042", Ct);
            if (rows.LastOrDefault(row => row.Manifest == manifest) is { } row)
            {
                return row.SequenceNumber;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    /// <summary>Waits until the listing has deleted what its snapshot holds.</summary>
    private static async Task EventsDeletedBeforeAsync(TemporaryDatabase database, long snapshot)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM journal
                WHERE persistence_id = 'charge-point-listing-OHM-SLC-0042' AND deleted = 0
                """;
            var snapshots = connection.CreateCommand();
            snapshots.CommandText = """
                SELECT COUNT(*) FROM snapshot
                WHERE persistence_id = 'charge-point-listing-OHM-SLC-0042'
                """;
            if ((long)(await command.ExecuteScalarAsync(Ct))! == 0
                && (long)(await snapshots.ExecuteScalarAsync(Ct))! == 1)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }

        throw new TimeoutException($"The listing did not settle on its snapshot at {snapshot}.");
    }

    private static async Task ExecuteAsync(TemporaryDatabase database, string sql)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }
}
