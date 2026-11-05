using System.Net;
using System.Net.Http.Json;
using Ohmline.Billing.Contracts.Holds;

namespace Ohmline.Billing.Tests.Holds;

/// <summary>Billing's hold API over HTTP, on a database of the test's own.</summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class HoldEndpointsTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_no_hold_when_one_is_placed_then_it_is_created_and_placed()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();
        var holdId = Known.NewHoldId();

        var placed = await http.PostAsJsonAsync("/holds", Request(holdId), Ct);

        placed.StatusCode.ShouldBe(HttpStatusCode.Created);
        placed.Headers.Location!.ToString().ShouldBe($"/holds/{holdId}");
        (await placed.Content.ReadFromJsonAsync<HoldResponse>(Ct))
            .ShouldBe(new HoldResponse(holdId, HoldStatus.Placed, 50m, "USD", null));
    }

    [Fact]
    public async Task Given_a_hold_when_it_is_placed_again_under_its_id_then_it_is_the_same_hold()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();
        var holdId = Known.NewHoldId();
        await http.PostAsJsonAsync("/holds", Request(holdId), Ct);

        var again = await http.PostAsJsonAsync("/holds", Request(holdId, amount: 80m), Ct);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<HoldResponse>(Ct))!.Amount.ShouldBe(50m);
    }

    [Fact]
    public async Task Given_a_hold_when_less_is_captured_then_that_much_is_taken_once()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();
        var holdId = Known.NewHoldId();
        await http.PostAsJsonAsync("/holds", Request(holdId), Ct);
        var capture = new CapturePaymentRequest(12.60m, "USD");

        var first = await http.PostAsJsonAsync($"/holds/{holdId}/capture", capture, Ct);
        var again = await http.PostAsJsonAsync($"/holds/{holdId}/capture", capture, Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<HoldResponse>(Ct))
            .ShouldBe(new HoldResponse(holdId, HoldStatus.Captured, 50m, "USD", 12.60m));
    }

    [Fact]
    public async Task Given_a_hold_when_more_than_it_is_captured_then_it_is_refused()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();
        var holdId = Known.NewHoldId();
        await http.PostAsJsonAsync("/holds", Request(holdId), Ct);

        var refused = await http.PostAsJsonAsync(
            $"/holds/{holdId}/capture", new CapturePaymentRequest(50.01m, "USD"), Ct);

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadFromJsonAsync<HoldProblem>(Ct))!.Code
            .ShouldBe(HoldProblemCodes.ExceedsHold);
    }

    [Fact]
    public async Task Given_no_hold_when_a_capture_comes_then_it_is_not_found()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();

        var refused = await http.PostAsJsonAsync(
            $"/holds/{Known.NewHoldId()}/capture", new CapturePaymentRequest(1m, "USD"), Ct);

        refused.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await refused.Content.ReadFromJsonAsync<HoldProblem>(Ct))!.Code
            .ShouldBe(HoldProblemCodes.UnknownHold);
    }

    [Fact]
    public async Task Given_a_voided_hold_then_nothing_can_be_captured_from_it()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();
        var holdId = Known.NewHoldId();
        await http.PostAsJsonAsync("/holds", Request(holdId), Ct);

        var voided = await http.PostAsync($"/holds/{holdId}/void", content: null, Ct);
        var refused = await http.PostAsJsonAsync(
            $"/holds/{holdId}/capture", new CapturePaymentRequest(1m, "USD"), Ct);

        (await voided.Content.ReadFromJsonAsync<HoldResponse>(Ct))!.Status
            .ShouldBe(HoldStatus.Voided);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadFromJsonAsync<HoldProblem>(Ct))!.Code
            .ShouldBe(HoldProblemCodes.HoldVoided);
    }

    [Fact]
    public async Task Given_a_card_that_is_declined_then_the_hold_is_refused_and_not_kept()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();
        var holdId = Known.NewHoldId();

        var refused = await http.PostAsJsonAsync(
            "/holds", new PlaceHoldRequest(holdId, "drv-declined", 50m, "USD"), Ct);

        refused.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);
        (await refused.Content.ReadFromJsonAsync<HoldProblem>(Ct))!.Code
            .ShouldBe(HoldProblemCodes.Declined);
        (await http.PostAsync($"/holds/{holdId}/void", content: null, Ct)).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Given_TallyPay_down_then_the_hold_is_unavailable_and_can_be_asked_again()
    {
        await using var billing = await StartAsync(new TallyPayServer().Failing(times: 4));
        var http = billing.CreateClient();
        var holdId = Known.NewHoldId();

        var unavailable = await http.PostAsJsonAsync("/holds", Request(holdId), Ct);
        var again = await http.PostAsJsonAsync("/holds", Request(holdId), Ct);

        unavailable.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await unavailable.Content.ReadFromJsonAsync<HoldProblem>(Ct))!.Code
            .ShouldBe(HoldProblemCodes.ProviderUnavailable);
        again.StatusCode.ShouldBe(HttpStatusCode.Created);
        billing.TallyPay.Requests.Select(request => request.Key).Distinct()
            .ShouldBe([holdId]);
    }

    [Fact]
    public async Task Given_a_placed_hold_then_its_capture_and_void_name_its_authorization()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();
        var (captured, voided) = (Known.NewHoldId(), Known.NewHoldId());
        await http.PostAsJsonAsync("/holds", Request(captured), Ct);
        await http.PostAsJsonAsync("/holds", Request(voided), Ct);

        await http.PostAsJsonAsync(
            $"/holds/{captured}/capture", new CapturePaymentRequest(12.60m, "USD"), Ct);
        await http.PostAsync($"/holds/{voided}/void", content: null, Ct);

        billing.TallyPay.Paths.ShouldBe(["/v1/preauth", "/v1/preauth", "/v1/capture", "/v1/void"]);
        billing.TallyPay.Requests.Select(request => request.Key).ShouldBe(
            [captured, voided, $"{captured}-capture", $"{voided}-void"]);
    }

    [Fact]
    public async Task Given_a_request_with_no_currency_and_no_amount_then_it_is_not_valid()
    {
        await using var billing = await StartAsync();
        var http = billing.CreateClient();

        var refused = await http.PostAsJsonAsync(
            "/holds", new PlaceHoldRequest(Known.NewHoldId(), "drv-7f3a91c2", 0m, "usd"), Ct);

        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<BillingService> StartAsync(TallyPayServer? tallyPay = null) =>
        new(await postgres.CreateDatabaseAsync(Ct), new FeedServer(), tallyPay);

    private static PlaceHoldRequest Request(string holdId, decimal amount = 50m) =>
        new(holdId, "drv-7f3a91c2", amount, "USD");
}
