using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Billing.Feed;
using Ohmline.Billing.Invoicing;

namespace Ohmline.Billing.Tests.Invoicing;

/// <summary>
/// The idempotent receiver: a completed session invoiced once and its receipt put in the outbox
/// once, however often the feed delivers it, with the position stored alongside.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class InvoiceIssuerTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_completed_session_then_it_is_invoiced_and_its_receipt_is_queued()
    {
        await using var database = await DatabaseAsync();
        var completed = Known.Completed();

        await new InvoiceIssuer(database, Clock)
            .HandleAsync(new CompletedSession(1, completed), Ct);

        var invoice = (await InvoiceStore.FindAsync(database, completed.SessionId, Ct))
            .ShouldNotBeNull();
        invoice.Total.ShouldBe(12.60m);
        invoice.Lines.ShouldHaveSingleItem().Code.ShouldBe("energy");
        (await OutboxAsync(database)).ShouldBe(["billing.receipt-issued.v1"]);
        (await Inbox.PositionAsync(database, Inbox.ChargingFeed, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Given_the_same_session_delivered_twice_then_it_is_billed_once()
    {
        await using var database = await DatabaseAsync();
        var issuer = new InvoiceIssuer(database, Clock);
        var completed = Known.Completed();

        await issuer.HandleAsync(new CompletedSession(4, completed), Ct);
        var redelivered = completed with { EventId = Guid.NewGuid() };
        await issuer.HandleAsync(new CompletedSession(9, redelivered), Ct);

        (await OutboxAsync(database)).ShouldHaveSingleItem();
        (await Inbox.PositionAsync(database, Inbox.ChargingFeed, Ct)).ShouldBe(9);
    }

    [Fact]
    public async Task Given_an_entry_Billing_skips_then_only_the_position_moves()
    {
        await using var database = await DatabaseAsync();

        await new InvoiceIssuer(database, Clock).HandleAsync(new SkippedEntry(5, "other.v1"), Ct);

        (await OutboxAsync(database)).ShouldBeEmpty();
        (await Inbox.PositionAsync(database, Inbox.ChargingFeed, Ct)).ShouldBe(5);
    }

    private static FakeTimeProvider Clock => new(Known.Morning.AddHours(1));

    private async Task<NpgsqlDataSource> DatabaseAsync() =>
        NpgsqlDataSource.Create(await postgres.CreateDatabaseAsync(Ct));

    private static async Task<List<string>> OutboxAsync(NpgsqlDataSource database)
    {
        await using var select = database.CreateCommand("SELECT type FROM billing.outbox");
        await using var reader = await select.ExecuteReaderAsync(Ct);
        var types = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            types.Add(reader.GetString(0));
        }

        return types;
    }
}
