using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// Rows in the pilot's shapes, replayed through the journal's read adapter (section 20.4): one
/// row becomes two of today's events, another becomes none, and the charge point recovers as
/// if it had recorded today's events itself.
/// </summary>
public sealed class LegacyReplayTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_rows_in_the_pilot_s_shapes_when_replayed_then_today_s_events_count()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using (var node = await SqliteNode.StartAsync(database, clock, Ct))
        {
            (await node.AskAsync<CommandReply>(Register(), Ct)).ShouldBe(new CommandAccepted(1));
        }

        var expiry = Morning + ChargePoint.ReservationHold;
        await AppendAsync(database, 2, "charging.reservation-taken-over.v1", $$"""
            {"connectorNumber":1,"previousTokenUid":"{{SomeoneElsesCard}}",
             "tokenUid":"{{DriversCard}}","expiresAt":"{{expiry:O}}",
             "chargePointId":"{{Charger}}","commandId":"{{Command}}",
             "occurredAt":"{{Morning:O}}"}
            """);
        await AppendAsync(database, 3, "charging.charge-point-heartbeat-recorded.v1", $$"""
            {"chargePointId":"{{Charger}}","commandId":"{{Command}}",
             "occurredAt":"{{Morning.AddMinutes(1):O}}"}
            """);

        await using var restarted = await SqliteNode.StartAsync(database, clock, Ct);
        var status = await restarted.AskAsync<ChargePointStatus>(Status(), Ct);

        status.Version.ShouldBe(3);
        status.Connectors[0].ShouldBe(new ConnectorStatus(
            1, new(22m), ConnectorStatus.Reserved, DriversCard, expiry));
        status.Connectors[1].State.ShouldBe(ConnectorStatus.Available);
    }

    /// <summary>A row as the pilot wrote it, with Charging's serializer id.</summary>
    private static async Task AppendAsync(
        TemporaryDatabase database, long sequenceNumber, string manifest, string json)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO journal (persistence_id, sequence_number, created, deleted,
                                 manifest, identifier, writer_uuid, message)
            VALUES ('charge-point-OHM-SLC-0042', $sequenceNumber, $created, 0,
                    $manifest, 9101, 'pilot', $message)
            """;
        insert.Parameters.AddWithValue("$sequenceNumber", sequenceNumber);
        insert.Parameters.AddWithValue("$created", DateTime.UtcNow.Ticks);
        insert.Parameters.AddWithValue("$manifest", manifest);
        insert.Parameters.AddWithValue("$message", Encoding.UTF8.GetBytes(json));
        await insert.ExecuteNonQueryAsync(Ct);
    }
}
