using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Billing.Feed;
using Ohmline.Billing.Invoicing;
using Ohmline.Billing.Outbox;
using Ohmline.Billing.Receipts;

namespace Ohmline.Billing.Tests.Outbox;

/// <summary>Billing's classic outbox: what is written with an invoice is sent after.</summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class OutboxDispatcherTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_an_invoice_then_its_receipt_is_sent_once()
    {
        await using var database = NpgsqlDataSource.Create(
            await postgres.CreateDatabaseAsync(Ct));
        var clock = new FakeTimeProvider(Known.Morning);
        var completed = Known.Completed();
        await new InvoiceIssuer(database, clock)
            .HandleAsync(new CompletedSession(1, completed), Ct);
        var sent = new SentReceipts();
        var dispatcher = new OutboxDispatcher(
            database, sent, clock, NullLogger<OutboxDispatcher>.Instance);

        var first = await dispatcher.DispatchAsync(Ct);
        var second = await dispatcher.DispatchAsync(Ct);

        (first, second).ShouldBe((1, 0));
        sent.Receipts.ShouldBe(
            [new ReceiptIssued(completed.SessionId, "04A2B3C4D5E6F7", 12.60m, "USD")]);
    }

    private sealed class SentReceipts : IReceiptSender
    {
        public List<ReceiptIssued> Receipts { get; } = [];

        public Task SendAsync(ReceiptIssued receipt, CancellationToken cancellationToken)
        {
            Receipts.Add(receipt);
            return Task.CompletedTask;
        }
    }
}
