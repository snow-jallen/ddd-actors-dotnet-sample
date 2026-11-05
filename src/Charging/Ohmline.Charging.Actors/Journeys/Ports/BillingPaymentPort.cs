using System.Net;
using System.Net.Http.Json;
using Ohmline.Billing.Contracts.Holds;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// The journey's payment port, behind which Billing takes the payments (section 16.5): Billing's
/// hold API, in Billing's contracts, translated into the journey's words. Charging is Billing's
/// customer here; Billing owns the API, and the journey's needs, such as idempotency, are on
/// Billing's backlog.
/// </summary>
/// <remarks>
/// A refusal is an answer: the journey hears <see cref="HoldDeclined"/> or
/// <see cref="CaptureFailed"/>. No answer is not: a timeout, an error or a 503 faults the task,
/// and the journey asks again (its reply timer for a hold, its capture retries for a capture,
/// section 15.5) under the same idempotency key. The hold is named by that key, so a hold asked
/// for twice is one hold, and a void needs nothing but the key.
/// </remarks>
public sealed class BillingPaymentPort(HttpClient billing) : IPaymentPort
{
    public async Task<PaymentPortResult> PlaceHoldAsync(
        IdempotencyKey key, DriverRef driver, Money amount, CancellationToken cancellationToken)
    {
        var request = new PlaceHoldRequest(
            key.Value, driver.Value, amount.Amount, amount.Currency.Code);
        using var response = await billing.PostAsJsonAsync("/holds", request, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.Created or HttpStatusCode.OK =>
                Placed(await ReadAsync<HoldResponse>(response, cancellationToken)),
            HttpStatusCode.PaymentRequired =>
                Declined(await ReadAsync<HoldProblem>(response, cancellationToken)),
            _ => throw NoAnswer(response),
        };
    }

    public async Task<PaymentPortResult> CaptureAsync(
        IdempotencyKey key, string holdId, Money amount, CancellationToken cancellationToken)
    {
        var request = new CapturePaymentRequest(amount.Amount, amount.Currency.Code);
        using var response = await billing.PostAsJsonAsync(
            $"/holds/{holdId}/capture", request, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.OK =>
                Captured(await ReadAsync<HoldResponse>(response, cancellationToken)),
            HttpStatusCode.PaymentRequired or HttpStatusCode.NotFound or HttpStatusCode.Conflict =>
                NotCaptured(holdId, await ReadAsync<HoldProblem>(response, cancellationToken)),
            _ => throw NoAnswer(response),
        };
    }

    /// <summary>A hold Billing never placed is a hold with nothing to release.</summary>
    public async Task<PaymentPortResult> VoidAsync(
        IdempotencyKey hold, CancellationToken cancellationToken)
    {
        using var response = await billing.PostAsync(
            $"/holds/{hold.Value}/void", content: null, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.OK or HttpStatusCode.NotFound => new HoldVoided(hold.Value),
            _ => throw NoAnswer(response),
        };
    }

    private static HoldPlaced Placed(HoldResponse hold) =>
        new(hold.HoldId, new Money(hold.Amount, new Currency(hold.Currency)));

    private static HoldDeclined Declined(HoldProblem problem) =>
        new(problem.Code ?? HoldProblemCodes.Declined, problem.Detail ?? "Declined.");

    private static PaymentCaptured Captured(HoldResponse hold) =>
        new(hold.HoldId, new Money(hold.Captured ?? 0m, new Currency(hold.Currency)));

    private static CaptureFailed NotCaptured(string holdId, HoldProblem problem) =>
        new(holdId, problem.Code ?? "refused", problem.Detail ?? "Billing refused the capture.");

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new HttpRequestException($"Billing answered {response.StatusCode}, empty.");

    private static HttpRequestException NoAnswer(HttpResponseMessage response) => new(
        $"Billing answered {(int)response.StatusCode}: no answer the journey can act on.",
        inner: null,
        response.StatusCode);
}
