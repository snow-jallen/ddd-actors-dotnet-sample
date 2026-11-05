using Npgsql;

namespace Ohmline.Charging.ReadModels.SessionHistory;

/// <summary>The SQL of <c>read_models.session_history</c>, as the projection uses it.</summary>
public static class SessionHistoryTable
{
    public static async Task<SessionHistoryRow?> FindAsync(
        NpgsqlTransaction transaction, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var select = new NpgsqlCommand(
            """
            SELECT session_id, token_uid, connector_id, status, energy_kwh, meter_start_wh,
                   started_at, ended_at, last_sequence_nr
            FROM read_models.session_history
            WHERE session_id = @session_id
            """,
            transaction.Connection,
            transaction);
        select.Parameters.AddWithValue("session_id", sessionId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SessionHistoryRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDecimal(4),
            reader.GetInt64(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetInt64(8));
    }

    /// <summary>
    /// Inserts the row, or replaces the stored one only if this one is newer: the guard on
    /// <c>last_sequence_nr</c> makes a late or repeated write change nothing.
    /// </summary>
    public static async Task UpsertAsync(
        NpgsqlTransaction transaction, SessionHistoryRow row, CancellationToken cancellationToken)
    {
        await using var upsert = new NpgsqlCommand(
            """
            INSERT INTO read_models.session_history AS current
                (session_id, token_uid, connector_id, status, energy_kwh, meter_start_wh,
                 started_at, ended_at, last_sequence_nr)
            VALUES (@session_id, @token_uid, @connector_id, @status, @energy_kwh,
                    @meter_start_wh, @started_at, @ended_at, @last_sequence_nr)
            ON CONFLICT (session_id) DO UPDATE SET
                status = excluded.status,
                energy_kwh = excluded.energy_kwh,
                ended_at = excluded.ended_at,
                last_sequence_nr = excluded.last_sequence_nr
            WHERE excluded.last_sequence_nr > current.last_sequence_nr
            """,
            transaction.Connection,
            transaction);
        upsert.Parameters.AddWithValue("session_id", row.SessionId);
        upsert.Parameters.AddWithValue("token_uid", row.TokenUid);
        upsert.Parameters.AddWithValue("connector_id", row.ConnectorId);
        upsert.Parameters.AddWithValue("status", row.Status);
        upsert.Parameters.AddWithValue("energy_kwh", row.EnergyKwh);
        upsert.Parameters.AddWithValue("meter_start_wh", row.MeterStartWh);
        upsert.Parameters.AddWithValue("started_at", row.StartedAt.ToUniversalTime());
        upsert.Parameters.AddWithValue(
            "ended_at", (object?)row.EndedAt?.ToUniversalTime() ?? DBNull.Value);
        upsert.Parameters.AddWithValue("last_sequence_nr", row.LastSequenceNr);
        await upsert.ExecuteNonQueryAsync(cancellationToken);
    }
}
