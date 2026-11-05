using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Ohmline.Charging.Contracts.Integration;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.ReadModels.Outbox;

/// <summary>
/// The SQL of the relay: the integration events waiting to be read,
/// <c>read_models.integration_outbox</c>, numbered in the order they were written, and the
/// sessions it has seen start, <c>read_models.integration_relay_sessions</c>.
/// </summary>
public static class OutboxTables
{
    /// <summary>
    /// Adds the event to the outbox, unless it is there: a second write of the same event, after
    /// a retry or a replay of the journal, changes nothing. Its position is the next number of
    /// the outbox's sequence.
    /// </summary>
    public static async Task AppendAsync(
        NpgsqlTransaction transaction,
        ChargingIntegrationEvent @event,
        CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO read_models.integration_outbox (event_id, type, occurred_at, payload)
            VALUES (@event_id, @type, @occurred_at, @payload)
            ON CONFLICT (event_id) DO NOTHING
            """,
            transaction.Connection,
            transaction);
        insert.Parameters.AddWithValue("event_id", @event.EventId);
        insert.Parameters.AddWithValue("type", WireNameOf(@event));
        insert.Parameters.AddWithValue("occurred_at", @event.OccurredAt);
        insert.Parameters.AddWithValue(
            "payload",
            NpgsqlDbType.Jsonb,
            JsonSerializer.Serialize(@event, PublishedLanguage.Json));
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<RelayedSession?> FindSessionAsync(
        NpgsqlTransaction transaction, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var select = new NpgsqlCommand(
            """
            SELECT token_uid, connector_id, started_at, currency, energy_rate, session_fees
            FROM read_models.integration_relay_sessions
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

        var currency = new Currency(reader.GetString(3));
        return new RelayedSession(
            sessionId,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            new Money(reader.GetDecimal(4), currency),
            new Money(reader.GetDecimal(5), currency));
    }

    public static async Task KeepSessionAsync(
        NpgsqlTransaction transaction, RelayedSession session, CancellationToken cancellationToken)
    {
        await using var upsert = new NpgsqlCommand(
            """
            INSERT INTO read_models.integration_relay_sessions
                (session_id, token_uid, connector_id, started_at, currency, energy_rate,
                 session_fees)
            VALUES (@session_id, @token_uid, @connector_id, @started_at, @currency,
                    @energy_rate, @session_fees)
            ON CONFLICT (session_id) DO NOTHING
            """,
            transaction.Connection,
            transaction);
        upsert.Parameters.AddWithValue("session_id", session.SessionId);
        upsert.Parameters.AddWithValue("token_uid", session.TokenUid);
        upsert.Parameters.AddWithValue("connector_id", session.ConnectorId);
        upsert.Parameters.AddWithValue("started_at", session.StartedAt);
        upsert.Parameters.AddWithValue("currency", session.EnergyRate.Currency.Code);
        upsert.Parameters.AddWithValue("energy_rate", session.EnergyRate.Amount);
        upsert.Parameters.AddWithValue("session_fees", session.SessionFees.Amount);
        await upsert.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task ForgetSessionAsync(
        NpgsqlTransaction transaction, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var delete = new NpgsqlCommand(
            "DELETE FROM read_models.integration_relay_sessions WHERE session_id = @session_id",
            transaction.Connection,
            transaction);
        delete.Parameters.AddWithValue("session_id", sessionId);
        await delete.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string WireNameOf(ChargingIntegrationEvent @event) => @event switch
    {
        SessionCompleted => SessionCompleted.WireName,
        SessionCompletedV2 => SessionCompletedV2.WireName,
    };
}
