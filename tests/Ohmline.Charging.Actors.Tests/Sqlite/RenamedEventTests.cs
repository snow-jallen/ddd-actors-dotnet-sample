using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Tests.JournalTour;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// Renaming a concept when events are forever (section 20.7). A connector reported faulted
/// before the rename was stored as <c>charging.connector-faulted.v1</c>; the charge point still
/// replays it, as the connector being out of order, and from now on writes the new name.
/// </summary>
public sealed class RenamedEventTests
{
    private const string PersistenceId = "charge-point-OHM-SLC-0042";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_journal_from_before_the_rename_then_it_replays_and_goes_on()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using (var node = await SqliteNode.StartAsync(database, clock, Ct))
        {
            (await node.AskAsync<CommandReply>(Register(), Ct)).ShouldBe(new CommandAccepted(1));
        }

        await AppendAsync(database, 2, Golden.Read("corpus/charging.connector-faulted.v1"));

        await using var restarted = await SqliteNode.StartAsync(database, clock, Ct);
        var status = await restarted.AskAsync<ChargePointStatus>(Status(), Ct);
        status.Version.ShouldBe(2);
        status.Connectors[1].ShouldBe(new ConnectorStatus(
            2, new(22m), ConnectorStatus.OutOfOrder, ErrorCode: "GroundFailure"));

        (await restarted.AskAsync<CommandReply>(
                new ReportConnectorOutOfOrder(Command, Charger, 1, "PowerSwitchFailure"), Ct))
            .ShouldBe(new CommandAccepted(3));
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        (await JournalTourQueries.ReadJournalAsync(connection, PersistenceId, Ct))
            .Select(row => row.Manifest)
            .ShouldBe(
            [
                "charging.charge-point-registered.v1",
                "charging.connector-faulted.v1",
                "charging.connector-out-of-order.v1",
            ]);
    }

    /// <summary>The event exactly as Charging stored it before section 20.7.</summary>
    private static async Task AppendAsync(
        TemporaryDatabase database, long sequenceNumber, string json)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO journal (persistence_id, sequence_number, created, deleted,
                                 manifest, identifier, writer_uuid, message)
            VALUES ($persistenceId, $sequenceNumber, $created, 0,
                    'charging.connector-faulted.v1', 9101, 'before-20.7', $message)
            """;
        insert.Parameters.AddWithValue("$persistenceId", PersistenceId);
        insert.Parameters.AddWithValue("$sequenceNumber", sequenceNumber);
        insert.Parameters.AddWithValue("$created", DateTime.UtcNow.Ticks);
        insert.Parameters.AddWithValue("$message", Encoding.UTF8.GetBytes(json));
        await insert.ExecuteNonQueryAsync(Ct);
    }
}
