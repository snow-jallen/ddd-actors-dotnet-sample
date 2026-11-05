using System.Net.Http.Json;
using System.Text.Json;

namespace Ohmline.Billing.TallyPay;

/// <summary>
/// TallyPay over HTTP: a typed client (section 16.4). Every call carries an idempotency key, so
/// the resilience handler may send a request again after a timeout or a 503, and TallyPay does
/// it once. Whatever happens, the caller gets a <see cref="PaymentOutcome"/>: no exception and
/// no TallyPay type leaves this class.
/// </summary>
public sealed class TallyPayClient(HttpClient http, ILogger<TallyPayClient> log)
{
    public const string IdempotencyKeyHeader = "X-Idempotency-Key";

    /// <summary>Reserve the amount on the driver's card, as <paramref name="holdId"/>.</summary>
    public Task<PaymentOutcome> PreauthorizeAsync(
        string holdId,
        string driverRef,
        decimal amount,
        string currency,
        CancellationToken cancellationToken) =>
        SendAsync(
            "/v1/preauth",
            holdId,
            new TallyPayPreauth(
                holdId, driverRef, TallyPayTranslator.ToMinorUnits(amount), currency),
            cancellationToken);

    public Task<PaymentOutcome> CaptureAsync(
        string holdId,
        string authorization,
        decimal amount,
        string currency,
        CancellationToken cancellationToken) =>
        SendAsync(
            "/v1/capture",
            $"{holdId}-capture",
            new TallyPayCapture(authorization, TallyPayTranslator.ToMinorUnits(amount), currency),
            cancellationToken);

    public Task<PaymentOutcome> VoidAsync(
        string holdId, string authorization, CancellationToken cancellationToken) =>
        SendAsync(
            "/v1/void", $"{holdId}-void", new TallyPayVoid(authorization), cancellationToken);

    private async Task<PaymentOutcome> SendAsync<TRequest>(
        string path, string idempotencyKey, TRequest body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Add(IdempotencyKeyHeader, idempotencyKey);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderUnavailable($"TallyPay answered {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<TallyPayReply>(cancellationToken)
                is { } reply
                ? TallyPayTranslator.Translate(reply)
                : new ProviderUnavailable("TallyPay answered with nothing.");
        }
        catch (Exception failure) when (
            failure is HttpRequestException or JsonException
            || (failure is OperationCanceledException or TimeoutException
                && !cancellationToken.IsCancellationRequested))
        {
            log.LogWarning(failure, "TallyPay did not answer {Path} ({Key})", path, idempotencyKey);
            return new ProviderUnavailable(failure.Message);
        }
    }
}
