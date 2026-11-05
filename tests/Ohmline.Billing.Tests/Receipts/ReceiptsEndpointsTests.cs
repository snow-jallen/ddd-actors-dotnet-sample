using System.Net;
using System.Net.Http.Json;
using Ohmline.Billing.Contracts.Receipts;

namespace Ohmline.Billing.Tests.Receipts;

/// <summary>
/// Billing as it runs: a session completed on the charging feed becomes a receipt, without
/// anyone asking for it.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class ReceiptsEndpointsTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_session_completed_on_the_feed_then_its_receipt_appears()
    {
        var completed = Known.Completed();
        await using var billing = new BillingService(
            await postgres.CreateDatabaseAsync(Ct), new FeedServer(completed));
        var http = billing.CreateClient();

        var receipt = await ReceiptAsync(http, completed.SessionId);

        receipt.Total.ShouldBe(12.60m);
        receipt.Currency.ShouldBe("USD");
        receipt.Lines.ShouldBe([new ReceiptLine("energy", 30m, "kWh", 0.42m, 12.60m)]);
        receipt.TokenUid.ShouldBe("04A2B3C4D5E6F7");
    }

    [Fact]
    public async Task Given_a_session_Billing_has_not_read_then_there_is_no_receipt_yet()
    {
        await using var billing = new BillingService(
            await postgres.CreateDatabaseAsync(Ct), new FeedServer());

        var answer = await billing.CreateClient().GetAsync($"/receipts/{Guid.NewGuid()}", Ct);

        answer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<ReceiptResponse> ReceiptAsync(HttpClient http, Guid session)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var answer = await http.GetAsync($"/receipts/{session}", Ct);
            if (answer.StatusCode == HttpStatusCode.OK)
            {
                return (await answer.Content.ReadFromJsonAsync<ReceiptResponse>(Ct))!;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No receipt for {session}: {answer.StatusCode}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }
}
