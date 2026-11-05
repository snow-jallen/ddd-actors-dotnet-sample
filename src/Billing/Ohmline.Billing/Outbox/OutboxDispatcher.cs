using Npgsql;
using Ohmline.Billing.Receipts;

namespace Ohmline.Billing.Outbox;

/// <summary>
/// Sends what Billing's outbox holds, oldest first, and marks each message sent in the same
/// database transaction that locked it. A crash after sending and before the commit sends the
/// message again: at least once, so a receiver of receipts must not mind a second copy.
/// </summary>
public sealed class OutboxDispatcher(
    NpgsqlDataSource database,
    IReceiptSender receipts,
    TimeProvider clock,
    ILogger<OutboxDispatcher> log)
    : BackgroundService
{
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await DispatchAsync(stoppingToken) > 0)
                {
                    continue;
                }
            }
            catch (Exception failure) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogWarning(failure, "Dispatching Billing's outbox failed; trying again");
            }

            await Task.Delay(Pause, clock, stoppingToken);
        }
    }

    /// <summary>
    /// Sends the next batch and says how many it sent. <c>SKIP LOCKED</c> lets two instances of
    /// Billing share the work without sending a message twice while both are up.
    /// </summary>
    public async Task<int> DispatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var batch = new List<(long Id, string Type, string Payload)>();
        await using (var select = new NpgsqlCommand(
            """
            SELECT id, type, payload FROM billing.outbox
            WHERE sent_at IS NULL
            ORDER BY id
            LIMIT 50
            FOR UPDATE SKIP LOCKED
            """,
            connection,
            transaction))
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                batch.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        foreach (var (id, type, payload) in batch)
        {
            await SendAsync(type, payload, cancellationToken);
            await using var sent = new NpgsqlCommand(
                "UPDATE billing.outbox SET sent_at = now() WHERE id = @id",
                connection,
                transaction);
            sent.Parameters.AddWithValue("id", id);
            await sent.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return batch.Count;
    }

    private Task SendAsync(string type, string payload, CancellationToken cancellationToken) =>
        type switch
        {
            ReceiptIssued.Type => receipts.SendAsync(
                BillingOutbox.Read<ReceiptIssued>(payload), cancellationToken),
            _ => throw new InvalidOperationException($"Billing's outbox holds a '{type}'."),
        };
}
