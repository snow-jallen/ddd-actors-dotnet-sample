using System.Collections.Immutable;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Ohmline.Billing.Contracts.Receipts;

namespace Ohmline.Billing.Invoicing;

/// <summary>The SQL of <c>billing.invoices</c>.</summary>
public static class InvoiceStore
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Adds the invoice, unless the session has one: false then, and nothing changes. This is
    /// what makes a session billed once however often the feed delivers it.
    /// </summary>
    public static async Task<bool> TryAddAsync(
        NpgsqlTransaction transaction, Invoice invoice, CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO billing.invoices
                (session_id, token_uid, connector_id, energy_kwh, started_at, ended_at,
                 currency, total, lines, issued_at)
            VALUES (@session_id, @token_uid, @connector_id, @energy_kwh, @started_at,
                    @ended_at, @currency, @total, @lines, @issued_at)
            ON CONFLICT (session_id) DO NOTHING
            """,
            transaction.Connection,
            transaction);
        insert.Parameters.AddWithValue("session_id", invoice.SessionId);
        insert.Parameters.AddWithValue("token_uid", invoice.TokenUid);
        insert.Parameters.AddWithValue("connector_id", invoice.ConnectorId);
        insert.Parameters.AddWithValue("energy_kwh", invoice.EnergyKwh);
        insert.Parameters.AddWithValue("started_at", invoice.StartedAt);
        insert.Parameters.AddWithValue("ended_at", invoice.EndedAt);
        insert.Parameters.AddWithValue("currency", invoice.Currency);
        insert.Parameters.AddWithValue("total", invoice.Total);
        insert.Parameters.AddWithValue(
            "lines", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(invoice.Lines, Web));
        insert.Parameters.AddWithValue("issued_at", invoice.IssuedAt);
        return await insert.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public static async Task<Invoice?> FindAsync(
        NpgsqlDataSource database, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var select = database.CreateCommand("""
            SELECT token_uid, connector_id, energy_kwh, started_at, ended_at, currency, total,
                   lines, issued_at
            FROM billing.invoices
            WHERE session_id = @session_id
            """);
        select.Parameters.AddWithValue("session_id", sessionId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Invoice(
            sessionId,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetString(5),
            reader.GetDecimal(6),
            JsonSerializer.Deserialize<ImmutableArray<ReceiptLine>>(reader.GetString(7), Web),
            reader.GetFieldValue<DateTimeOffset>(8));
    }
}
