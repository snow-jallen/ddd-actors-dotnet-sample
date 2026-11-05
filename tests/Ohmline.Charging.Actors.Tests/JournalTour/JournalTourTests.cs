using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Tests.Sqlite;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.JournalTour;

/// <summary>
/// What lands in SQLite when charge point OHM-SLC-0042 accepts two commands and rejects a
/// third, read straight from the tables. The rows are written to the test output; the tour in
/// <c>docs/journal-tour.md</c> quotes them.
/// </summary>
public sealed class JournalTourTests(ITestOutputHelper output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string PersistenceId => ChargePointActor.PersistenceIdOf(Charger);

    [Fact]
    public async Task Given_two_accepted_commands_and_a_rejected_one_then_there_are_two_rows()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await SqliteNode.StartAsync(database, clock, Ct);
        var before = DateTimeOffset.UtcNow;

        var (register, reserve) = (Register(), Reserve(1, DriversCard));
        (await node.AskAsync<CommandReply>(register, Ct)).ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<CommandReply>(reserve, Ct)).ShouldBe(new CommandAccepted(2));
        (await node.AskAsync<CommandReply>(Occupy(1, SomeoneElsesCard), Ct))
            .ShouldBeOfType<CommandRejected>().Code.ShouldBe("reserved-for-another-token");

        await using var connection = await OpenAsync(database);
        var rows = await JournalTourQueries.ReadJournalAsync(connection, PersistenceId, Ct);
        await Show(connection, rows);

        rows.Select(row => row.SequenceNumber).ShouldBe([1, 2]); // Nothing for the rejection.
        rows.ShouldAllBe(row => row.PersistenceId == "charge-point-OHM-SLC-0042");
        rows.Select(row => row.Manifest).ShouldBe(
            ["charging.charge-point-registered.v1", "charging.connector-reserved.v1"]);
        rows.ShouldAllBe(row => row.Identifier == 9101 && !row.Deleted);
        rows.ShouldAllBe(row => row.CreatedAt >= before && row.CreatedAt <= DateTimeOffset.UtcNow);
        rows.Select(row => row.WriterUuid).Distinct().ShouldHaveSingleItem().ShouldNotBeEmpty();
        rows.Select(row => node.System.Serialization.Deserialize(
                row.Message, row.Identifier, row.Manifest))
            .ShouldBe(
            [
                Registered(by: register.CommandId),
                Reserved(1, DriversCard, by: reserve.CommandId),
            ]);
        (await Lines(connection, JournalTourQueries.Tags)).ShouldBeEmpty();
        (await Lines(connection, JournalTourQueries.Snapshots)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_tables_are_journal_tags_and_snapshot_and_journal_has_no_tags_column()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await SqliteNode.StartAsync(database, clock, Ct);
        await node.AskAsync<CommandReply>(Register(), Ct); // The journal creates its tables lazily.

        await using var connection = await OpenAsync(database);
        var schema = await Lines(connection, JournalTourQueries.Schema);
        foreach (var line in schema)
        {
            output.WriteLine(line);
        }

        schema.ShouldContain(line => line.StartsWith("name=journal |"));
        schema.ShouldContain(line => line.StartsWith("name=tags |"));
        schema.ShouldContain(line => line.StartsWith("name=snapshot |"));
        schema.ShouldContain(line => line.Contains(
            "CREATE UNIQUE INDEX journal_uq ON journal (persistence_id, sequence_number)"));
        schema.Single(line => line.StartsWith("name=journal |")).ShouldNotContain("[tags]");
    }

    [Fact]
    public async Task Given_200_events_then_one_snapshot_row_remains_and_every_event_stays()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await SqliteNode.StartAsync(database, clock, Ct);
        var before = DateTimeOffset.UtcNow;

        await node.AskAsync<CommandReply>(Register(), Ct);
        for (var version = 2; version <= 200; version++)
        {
            await node.AskAsync<CommandReply>(
                version % 2 == 0 ? Reserve(1, DriversCard) : Cancel(1, DriversCard), Ct);
        }

        await using var connection = await OpenAsync(database);
        var snapshot = (await SnapshotsOnceSettledAsync(connection, 200)).ShouldHaveSingleItem();
        output.WriteLine("snapshot:");
        output.WriteLine(snapshot.ToString());

        snapshot.PersistenceId.ShouldBe("charge-point-OHM-SLC-0042");
        snapshot.SequenceNumber.ShouldBe(200); // The one at 100 was deleted.
        snapshot.Manifest.ShouldBe("charging.charge-point-snapshot.v1");
        snapshot.SerializerId.ShouldBe(9101);
        snapshot.CreatedAt.ShouldBeInRange(before, DateTimeOffset.UtcNow);
        node.System.Serialization
            .Deserialize(snapshot.Snapshot, snapshot.SerializerId, snapshot.Manifest)
            .ShouldBeOfType<ChargePointSnapshot>()
            .ToState()
            .ShouldBe(ChargePoint.Fold(
                await JournalProbe.EventsAsync<ChargePointEvent>(node.System, PersistenceId, Ct)));
        (await JournalTourQueries.ReadJournalAsync(connection, PersistenceId, Ct))
            .Select(row => row.SequenceNumber)
            .ShouldBe(Enumerable.Range(1, 200).Select(number => (long)number));
    }

    /// <summary>
    /// Snapshots are saved and old ones deleted in the background, so the test reads the table
    /// until only the expected one is left, or gives up and lets the assertions say why.
    /// </summary>
    private static async Task<IReadOnlyList<SnapshotRow>> SnapshotsOnceSettledAsync(
        SqliteConnection connection, long sequenceNumber)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5 * ActorTestConfig.TimeFactor);
        while (true)
        {
            var rows = await JournalTourQueries.ReadSnapshotsAsync(connection, PersistenceId, Ct);
            if (rows is [{ } only] && only.SequenceNumber == sequenceNumber
                || DateTimeOffset.UtcNow > deadline)
            {
                return rows;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    private static ChargePointEvent Stamped(ChargePointEvent @event) =>
        @event with { CommandId = Command, OccurredAt = Morning };

    private static async Task<SqliteConnection> OpenAsync(TemporaryDatabase database)
    {
        var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        return connection;
    }

    private static Task<IReadOnlyList<string>> Lines(SqliteConnection connection, string query) =>
        JournalTourQueries.ReadLinesAsync(connection, query, PersistenceId, Ct);

    private async Task Show(SqliteConnection connection, IReadOnlyList<JournalRow> rows)
    {
        output.WriteLine("journal:");
        foreach (var row in rows)
        {
            output.WriteLine(row.ToString());
        }

        output.WriteLine($"tags: {(await Lines(connection, JournalTourQueries.Tags)).Count} rows");
        output.WriteLine(
            $"snapshot: {(await Lines(connection, JournalTourQueries.Snapshots)).Count} rows");
    }
}
