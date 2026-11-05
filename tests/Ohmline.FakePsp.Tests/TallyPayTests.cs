using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ohmline.FakePsp.Tests;

/// <summary>The fake TallyPay as Billing meets it: over HTTP, in TallyPay's own terms.</summary>
public sealed class TallyPayTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_card_then_a_preauthorization_is_approved_with_a_business_date()
    {
        await using var tallyPay = new WebApplicationFactory<Program>();

        var reply = await PostAsync(tallyPay.CreateClient(), "/v1/preauth", "k-1", Preauth());

        reply.StatusCode.ShouldBe("00");
        reply.AuthorizationId.ShouldNotBeNullOrEmpty();
        reply.TransactionDate.ShouldMatch("^[0-9]{8}$");
    }

    [Fact]
    public async Task Given_the_same_idempotency_key_then_the_first_answer_comes_again()
    {
        await using var tallyPay = new WebApplicationFactory<Program>();
        var http = tallyPay.CreateClient();

        var first = await PostAsync(http, "/v1/preauth", "k-2", Preauth());
        var again = await PostAsync(http, "/v1/preauth", "k-2", Preauth());
        var other = await PostAsync(http, "/v1/preauth", "k-3", Preauth());

        again.ShouldBe(first);
        other.AuthorizationId.ShouldNotBe(first.AuthorizationId);
    }

    [Theory]
    [InlineData("drv-declined", 5000, "05")]
    [InlineData("drv-expired1", 5000, "54")]
    [InlineData("drv-7f3a91c2", 50001, "51")]
    public async Task Some_cards_and_amounts_are_refused(
        string card, long amountMinor, string status)
    {
        await using var tallyPay = new WebApplicationFactory<Program>();

        var reply = await PostAsync(
            tallyPay.CreateClient(), "/v1/preauth", "k-8", Preauth(card, amountMinor));

        reply.StatusCode.ShouldBe(status);
    }

    [Fact]
    public async Task Given_an_authorization_then_no_more_than_it_can_be_captured()
    {
        await using var tallyPay = new WebApplicationFactory<Program>();
        var http = tallyPay.CreateClient();
        var authorization = (await PostAsync(http, "/v1/preauth", "k-4", Preauth()))
            .AuthorizationId;

        var tooMuch = await PostAsync(http, "/v1/capture", "k-5", Capture(authorization, 5001));
        var enough = await PostAsync(http, "/v1/capture", "k-6", Capture(authorization, 1260));
        var voided = await PostAsync(http, "/v1/void", "k-7", new { AUTH_ID = authorization });

        (tooMuch.StatusCode, enough.StatusCode, voided.StatusCode).ShouldBe(("13", "00", "12"));
    }

    [Fact]
    public async Task Given_a_flaky_rate_of_one_then_every_request_is_unavailable()
    {
        await using var tallyPay = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host => host.UseSetting("FLAKY_RATE", "1"));

        var answer = await tallyPay.CreateClient()
            .PostAsJsonAsync("/v1/preauth", Preauth(), Ct);

        answer.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    private static object Preauth(string card = "drv-7f3a91c2", long amountMinor = 5000) =>
        new { MERCHANT_REF = "019a-hold", CARD_TOKEN = card, AMT_MINOR = amountMinor, CCY = "USD" };

    private static object Capture(string? authorization, long amountMinor) =>
        new { AUTH_ID = authorization, AMT_MINOR = amountMinor, CCY = "USD" };

    private static async Task<TallyPayReply> PostAsync(
        HttpClient http, string path, string key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Idempotency-Key", key);
        var answer = await http.SendAsync(request, Ct);
        answer.EnsureSuccessStatusCode();
        return (await answer.Content.ReadFromJsonAsync<TallyPayReply>(Ct))!;
    }
}
