using Npgsql;

namespace Ohmline.Charging.ReadModels.SessionHistory;

/// <summary>The SQL of <c>read_models.session_history_v2</c>, as its projection uses it.</summary>
public static class SessionHistoryV2Table
{
    public static async Task<SessionHistoryV2Row?> FindAsync(
        NpgsqlTransaction transaction, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var select = new NpgsqlCommand(
            """
            SELECT session_id, site_id, token_uid, connector_id, status, energy_kwh,
                   meter_start_wh, phase_count, started_at, ended_at, last_sequence_nr
            FROM read_models.session_history_v2
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

        var session = new SessionHistoryRow(
            reader.GetGuid(0),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetDecimal(5),
            reader.GetInt64(6),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetInt64(10));
        return new SessionHistoryV2Row(
            session, reader.GetString(1), reader.IsDBNull(7) ? null : reader.GetInt32(7));
    }

    /// <summary>
    /// Inserts the row, or replaces the stored one only if this one is newer, as the first
    /// version does.
    /// </summary>
    public static async Task UpsertAsync(
        NpgsqlTransaction transaction, SessionHistoryV2Row row, CancellationToken cancellationToken)
    {
        await using var upsert = new NpgsqlCommand(
            """
            INSERT INTO read_models.session_history_v2 AS current
                (session_id, site_id, token_uid, connector_id, status, energy_kwh,
                 meter_start_wh, phase_count, started_at, ended_at, last_sequence_nr)
            VALUES (@session_id, @site_id, @token_uid, @connector_id, @status, @energy_kwh,
                    @meter_start_wh, @phase_count, @started_at, @ended_at, @last_sequence_nr)
            ON CONFLICT (session_id) DO UPDATE SET
                status = excluded.status,
                energy_kwh = excluded.energy_kwh,
                ended_at = excluded.ended_at,
                last_sequence_nr = excluded.last_sequence_nr
            WHERE excluded.last_sequence_nr > current.last_sequence_nr
            """,
            transaction.Connection,
            transaction);
        var session = row.Session;
        upsert.Parameters.AddWithValue("session_id", session.SessionId);
        upsert.Parameters.AddWithValue("site_id", row.SiteId);
        upsert.Parameters.AddWithValue("token_uid", session.TokenUid);
        upsert.Parameters.AddWithValue("connector_id", session.ConnectorId);
        upsert.Parameters.AddWithValue("status", session.Status);
        upsert.Parameters.AddWithValue("energy_kwh", session.EnergyKwh);
        upsert.Parameters.AddWithValue("meter_start_wh", session.MeterStartWh);
        upsert.Parameters.AddWithValue("phase_count", (object?)row.PhaseCount ?? DBNull.Value);
        upsert.Parameters.AddWithValue("started_at", session.StartedAt.ToUniversalTime());
        upsert.Parameters.AddWithValue(
            "ended_at", (object?)session.EndedAt?.ToUniversalTime() ?? DBNull.Value);
        upsert.Parameters.AddWithValue("last_sequence_nr", session.LastSequenceNr);
        await upsert.ExecuteNonQueryAsync(cancellationToken);
    }
}
