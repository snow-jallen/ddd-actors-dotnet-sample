using System.Text;
using Akka.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// A session whose reading was stored before section 20.3, in Wh under
/// <c>charging.meter-value-recorded.v1</c>, recovers on today's code: the serializer upcasts the
/// row as it is replayed, and the session ends with the energy the reading delivered.
/// </summary>
public sealed class UpcastReplayTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_reading_stored_as_v1_when_the_session_recovers_then_it_counts()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using (var node = await SqliteNode.StartAsync(database, clock, Ct))
        {
            (await node.AskAsync<ChargingSessionRegion, CommandReply>(Begin(), Ct))
                .ShouldBe(new CommandAccepted(1));
        }

        await AppendV1ReadingAsync(database, sequenceNumber: 2, registerWh: 1_207_400);

        await using var restarted = await SqliteNode.StartAsync(database, clock, Ct);
        (await restarted.AskAsync<ChargingSessionRegion, CommandReply>(
                new StopSession(Command, Session), Ct))
            .ShouldBe(new CommandAccepted(3));
        var ended = await ReadEventAsync(restarted, database, sequenceNumber: 3);
        ended.ShouldBeOfType<SessionEnded>().EnergyDelivered.ShouldBe(new Energy(7.4m));
    }

    /// <summary>A reading exactly as section 10.10's code wrote it, before section 20.3.</summary>
    private static async Task AppendV1ReadingAsync(
        TemporaryDatabase database, long sequenceNumber, long registerWh)
    {
        var v1 = $$"""
            {"registerWh":{{registerWh}},"measuredAt":"2026-11-04T14:50:00+00:00",
             "sessionId":"{{Session}}","commandId":"{{Command}}",
             "occurredAt":"2026-11-04T14:50:00+00:00"}
            """;
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO journal (persistence_id, sequence_number, created, deleted,
                                 manifest, identifier, writer_uuid, message)
            VALUES ($persistenceId, $sequenceNumber, $created, 0,
                    'charging.meter-value-recorded.v1', 9101, 'by-hand', $message)
            """;
        insert.Parameters.AddWithValue("$persistenceId", $"charging-session-{Session}");
        insert.Parameters.AddWithValue("$sequenceNumber", sequenceNumber);
        insert.Parameters.AddWithValue("$created", DateTime.UtcNow.Ticks);
        insert.Parameters.AddWithValue("$message", Encoding.UTF8.GetBytes(v1));
        await insert.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<object> ReadEventAsync(
        SqliteNode node, TemporaryDatabase database, long sequenceNumber)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var select = connection.CreateCommand();
        select.CommandText = """
            SELECT identifier, manifest, message FROM journal
            WHERE persistence_id = $persistenceId AND sequence_number = $sequenceNumber
            """;
        select.Parameters.AddWithValue("$persistenceId", $"charging-session-{Session}");
        select.Parameters.AddWithValue("$sequenceNumber", sequenceNumber);
        await using var row = await select.ExecuteReaderAsync(Ct);
        (await row.ReadAsync(Ct)).ShouldBeTrue();
        return node.System.Serialization.Deserialize(
            (byte[])row.GetValue(2), row.GetInt32(0), row.GetString(1));
    }
}
