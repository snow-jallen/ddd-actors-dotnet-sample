using System.Net;
using System.Net.Http.Json;
using Ohmline.Billing.Contracts.Holds;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// The payment port against Billing's hold API, as Billing answers it: each answer in
/// Billing's contracts becomes the journey's input, and no answer becomes a failed task.
/// </summary>
public sealed class BillingPaymentPortTests
{
    private static readonly IdempotencyKey HoldKey = IdempotencyKey.Of(TheJourney.Journey, "hold");

    private static readonly IdempotencyKey CaptureKey =
        IdempotencyKey.Of(TheJourney.Journey, "capture");

    private static readonly Money Fifty = new(50m, Currency.Usd);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_hold_Billing_places_then_the_journey_hears_it_placed_under_its_key()
    {
        var billing = new Billing(HttpStatusCode.Created, Placed());

        var result = await Port(billing).PlaceHoldAsync(
            HoldKey, new DriverRef("drv-7f3a91c2"), Fifty, Ct);

        result.ShouldBe(new HoldPlaced(HoldKey.Value, Fifty));
        var (path, body) = billing.Requests.ShouldHaveSingleItem();
        path.ShouldBe("/holds");
        body.ShouldBe(new PlaceHoldRequest(HoldKey.Value, "drv-7f3a91c2", 50m, "USD"));
    }

    [Fact]
    public async Task Given_a_card_Billing_declines_then_the_journey_hears_it_declined()
    {
        var problem = new HoldProblem(402, "declined", "The card's issuer refused the payment.");
        var billing = new Billing(HttpStatusCode.PaymentRequired, problem);

        var result = await Port(billing).PlaceHoldAsync(
            HoldKey, new DriverRef("drv-declined"), Fifty, Ct);

        result.ShouldBe(new HoldDeclined("declined", "The card's issuer refused the payment."));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Given_no_answer_from_Billing_then_the_task_fails_and_the_journey_asks_again(
        HttpStatusCode status)
    {
        var billing = new Billing(status, new HoldProblem((int)status, "provider-unavailable", ""));

        await Should.ThrowAsync<HttpRequestException>(() => Port(billing).PlaceHoldAsync(
            HoldKey, new DriverRef("drv-7f3a91c2"), Fifty, Ct));
    }

    [Fact]
    public async Task Given_a_capture_Billing_takes_then_the_journey_hears_what_was_captured()
    {
        var billing = new Billing(HttpStatusCode.OK, Placed() with
        {
            Status = HoldStatus.Captured,
            Captured = 12.60m,
        });

        var result = await Port(billing).CaptureAsync(
            CaptureKey, HoldKey.Value, new Money(12.60m, Currency.Usd), Ct);

        result.ShouldBe(new PaymentCaptured(HoldKey.Value, new Money(12.60m, Currency.Usd)));
        billing.Requests.ShouldHaveSingleItem().Path.ShouldBe($"/holds/{HoldKey.Value}/capture");
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "exceeds-hold")]
    [InlineData(HttpStatusCode.NotFound, "unknown-hold")]
    [InlineData(HttpStatusCode.PaymentRequired, "declined")]
    public async Task Given_a_capture_Billing_refuses_then_the_journey_hears_it_failed(
        HttpStatusCode status, string code)
    {
        var billing = new Billing(status, new HoldProblem((int)status, code, "Refused."));

        var result = await Port(billing).CaptureAsync(CaptureKey, HoldKey.Value, Fifty, Ct);

        result.ShouldBe(new CaptureFailed(HoldKey.Value, code, "Refused."));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task A_void_names_the_hold_by_its_key_and_a_hold_never_placed_is_voided_too(
        HttpStatusCode status)
    {
        var billing = new Billing(status, Placed() with { Status = HoldStatus.Voided });

        var result = await Port(billing).VoidAsync(HoldKey, Ct);

        result.ShouldBe(new HoldVoided(HoldKey.Value));
        billing.Requests.ShouldHaveSingleItem().Path.ShouldBe($"/holds/{HoldKey.Value}/void");
    }

    private static HoldResponse Placed() => new(HoldKey.Value, HoldStatus.Placed, 50m, "USD", null);

    private static BillingPaymentPort Port(Billing billing) =>
        new(new HttpClient(billing) { BaseAddress = new Uri("http://billing") });

    /// <summary>Billing's hold API, answering every request with one status and body.</summary>
    private sealed class Billing(HttpStatusCode status, object answer) : HttpMessageHandler
    {
        public List<(string Path, PlaceHoldRequest? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null || request.RequestUri!.AbsolutePath != "/holds"
                ? null
                : await request.Content.ReadFromJsonAsync<PlaceHoldRequest>(cancellationToken);
            Requests.Add((request.RequestUri!.AbsolutePath, body));
            return new HttpResponseMessage(status) { Content = JsonContent.Create(answer) };
        }
    }
}
