using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Billing.Feed;
using Ohmline.Billing.Invoicing;

namespace Ohmline.Billing.Tests.Feed;

/// <summary>
/// The feed consumer against a stand-in for Charging's feed: each page read from the position
/// Billing stored, each completed session invoiced once.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class ChargingFeedConsumerTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_feed_then_each_page_is_read_from_the_position_stored_last()
    {
        await using var database = NpgsqlDataSource.Create(
            await postgres.CreateDatabaseAsync(Ct));
        var first = Known.Completed();
        var second = Known.Completed();
        var feed = new FeedServer(first, second).ThenUnknown("ohmline.charging.something-new.v1");
        var consumer = Consumer(database, feed, pageSize: 2);

        var read = new[]
        {
            await consumer.ReadPageAsync(Ct),
            await consumer.ReadPageAsync(Ct),
            await consumer.ReadPageAsync(Ct),
        };

        read.ShouldBe([2, 1, 0]);
        (await Inbox.PositionAsync(database, Inbox.ChargingFeed, Ct)).ShouldBe(3);
        (await InvoiceStore.FindAsync(database, first.SessionId, Ct)).ShouldNotBeNull();
        (await InvoiceStore.FindAsync(database, second.SessionId, Ct)).ShouldNotBeNull();
    }

    private static ChargingFeedConsumer Consumer(
        NpgsqlDataSource database, FeedServer feed, int pageSize)
    {
        var clock = new FakeTimeProvider(Known.Morning);
        var http = new HttpClient(feed) { BaseAddress = new Uri("http://node") };
        return new ChargingFeedConsumer(
            new ChargingFeedClient(http),
            new InvoiceIssuer(database, clock),
            database,
            Options.Create(new ChargingFeedOptions { PageSize = pageSize }),
            clock,
            NullLogger<ChargingFeedConsumer>.Instance);
    }
}
