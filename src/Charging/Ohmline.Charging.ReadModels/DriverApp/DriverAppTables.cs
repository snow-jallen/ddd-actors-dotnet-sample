using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Ohmline.Charging.ReadModels.DriverApp;

/// <summary>
/// The SQL of the driver app's read model: <c>read_models.driver_app_cards</c>, one row per
/// session, and each token's change feed, <c>read_models.driver_app_feed</c>.
/// </summary>
public static class DriverAppTables
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static async Task<DriverAppCard?> FindCardAsync(
        NpgsqlTransaction transaction, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var select = new NpgsqlCommand(
            """
            SELECT token_uid, session_id, status, connector_id, charge_point_display_name,
                   energy_kwh, meter_start_wh, started_at, ended_at, version
            FROM read_models.driver_app_cards
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

        return new DriverAppCard(
            reader.GetString(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetDecimal(5),
            reader.GetInt64(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetInt64(9));
    }

    /// <summary>
    /// Writes the card, unless the stored one is as new: the guard on <c>version</c> makes a
    /// late or repeated write change nothing.
    /// </summary>
    public static async Task UpsertCardAsync(
        NpgsqlTransaction transaction, DriverAppCard card, CancellationToken cancellationToken)
    {
        await using var upsert = new NpgsqlCommand(
            """
            INSERT INTO read_models.driver_app_cards AS current
                (token_uid, session_id, status, connector_id, charge_point_display_name,
                 energy_kwh, meter_start_wh, started_at, ended_at, version)
            VALUES (@token_uid, @session_id, @status, @connector_id, @display_name,
                    @energy_kwh, @meter_start_wh, @started_at, @ended_at, @version)
            ON CONFLICT (token_uid, session_id) DO UPDATE SET
                status = excluded.status,
                charge_point_display_name = excluded.charge_point_display_name,
                energy_kwh = excluded.energy_kwh,
                ended_at = excluded.ended_at,
                version = excluded.version
            WHERE excluded.version > current.version
            """,
            transaction.Connection,
            transaction);
        upsert.Parameters.AddWithValue("token_uid", card.TokenUid);
        upsert.Parameters.AddWithValue("session_id", card.SessionId);
        upsert.Parameters.AddWithValue("status", card.Status);
        upsert.Parameters.AddWithValue("connector_id", card.ConnectorId);
        upsert.Parameters.AddWithValue(
            "display_name", (object?)card.ChargePointDisplayName ?? DBNull.Value);
        upsert.Parameters.AddWithValue("energy_kwh", card.EnergyKwh);
        upsert.Parameters.AddWithValue("meter_start_wh", card.MeterStartWh);
        upsert.Parameters.AddWithValue("started_at", card.StartedAt);
        upsert.Parameters.AddWithValue("ended_at", (object?)card.EndedAt ?? DBNull.Value);
        upsert.Parameters.AddWithValue("version", card.Version);
        await upsert.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Appends the card to its token's change feed and returns the entry's position. The
    /// projection is one stream, which handles one event at a time, so positions are committed
    /// in order: an app that has read up to a position never later finds an entry below it. A
    /// rolled-back append leaves a gap, which a reader never notices.
    /// </summary>
    public static async Task<long> AppendToFeedAsync(
        NpgsqlTransaction transaction, DriverAppCard card, CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO read_models.driver_app_feed (token_uid, session_id, version, payload)
            VALUES (@token_uid, @session_id, @version, @payload)
            RETURNING position
            """,
            transaction.Connection,
            transaction);
        insert.Parameters.AddWithValue("token_uid", card.TokenUid);
        insert.Parameters.AddWithValue("session_id", card.SessionId);
        insert.Parameters.AddWithValue("version", card.Version);
        insert.Parameters.AddWithValue(
            "payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(card, Web));
        return (long)(await insert.ExecuteScalarAsync(cancellationToken))!;
    }
}
