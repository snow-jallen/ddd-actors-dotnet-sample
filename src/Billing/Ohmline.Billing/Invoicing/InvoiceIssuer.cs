using Npgsql;
using Ohmline.Billing.Feed;
using Ohmline.Billing.Outbox;
using Ohmline.Billing.Receipts;

namespace Ohmline.Billing.Invoicing;

/// <summary>
/// Billing's idempotent receiver for the charging feed (section 16.3): each entry is handled
/// in one database transaction that also stores its position. A completed session becomes an
/// invoice and, in the same transaction, a receipt in the outbox. An entry delivered again finds
/// the invoice there and changes nothing but the position.
/// </summary>
public sealed class InvoiceIssuer(NpgsqlDataSource database, TimeProvider clock)
{
    public async Task HandleAsync(FeedItem item, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (item.Value is CompletedSession completed)
        {
            var invoice = Invoice.From(completed.Event, clock.GetUtcNow());
            if (await InvoiceStore.TryAddAsync(transaction, invoice, cancellationToken))
            {
                await BillingOutbox.AppendAsync(
                    transaction,
                    ReceiptIssued.Type,
                    new ReceiptIssued(
                        invoice.SessionId, invoice.TokenUid, invoice.Total, invoice.Currency),
                    cancellationToken);
            }
        }

        await Inbox.MoveToAsync(
            transaction, Inbox.ChargingFeed, item.Position, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
