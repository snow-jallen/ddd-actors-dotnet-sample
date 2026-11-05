using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Ohmline.Billing.Outbox;

/// <summary>
/// Billing's classic transactional outbox (section 16.3): <c>billing.outbox</c>. A message is
/// written in the same database transaction as the change it announces, so there is never a
/// receipt without an invoice or an invoice whose receipt is forgotten. The dispatcher sends it
/// afterwards.
/// </summary>
public static class BillingOutbox
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static async Task AppendAsync<TMessage>(
        NpgsqlTransaction transaction,
        string type,
        TMessage message,
        CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand(
            "INSERT INTO billing.outbox (type, payload) VALUES (@type, @payload)",
            transaction.Connection,
            transaction);
        insert.Parameters.AddWithValue("type", type);
        insert.Parameters.AddWithValue(
            "payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(message, Web));
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public static T Read<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload, Web)
            ?? throw new JsonException("An outbox message has no payload.");
}
