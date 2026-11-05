using Microsoft.AspNetCore.Http.HttpResults;

namespace Ohmline.FakePsp;

/// <summary>
/// TallyPay's three calls. Each may carry an <c>X-Idempotency-Key</c>; TallyPay answers a key it
/// has seen with the answer it gave the first time. A flaky request answers 503 before anything
/// happens, so asking again is always safe.
/// </summary>
public static class TallyPayEndpoints
{
    private const string IdempotencyKey = "X-Idempotency-Key";

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapTallyPay()
        {
            var v1 = endpoints.MapGroup("/v1");
            v1.MapPost("/preauth", (PreauthRequest request, HttpRequest http,
                    TallyPayLedger ledger, Flakiness flaky) =>
                Answer(flaky, () => ledger.Preauthorize(KeyOf(http), request)));
            v1.MapPost("/capture", (CaptureRequest request, HttpRequest http,
                    TallyPayLedger ledger, Flakiness flaky) =>
                Answer(flaky, () => ledger.Capture(KeyOf(http), request)));
            v1.MapPost("/void", (VoidRequest request, HttpRequest http,
                    TallyPayLedger ledger, Flakiness flaky) =>
                Answer(flaky, () => ledger.Void(KeyOf(http), request)));
            return endpoints;
        }
    }

    private static Results<Ok<TallyPayReply>, StatusCodeHttpResult> Answer(
        Flakiness flaky, Func<TallyPayReply> reply) =>
        flaky.Strikes()
            ? TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable)
            : TypedResults.Ok(reply());

    private static string? KeyOf(HttpRequest http) =>
        http.Headers[IdempotencyKey].FirstOrDefault();
}
