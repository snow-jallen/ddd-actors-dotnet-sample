using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Billing.Contracts.Holds;
using Ohmline.Billing.TallyPay;

namespace Ohmline.Billing.Holds;

/// <summary>
/// The hold API (section 16.3): transaction scripts over <c>billing.holds</c>. Each request
/// reads the hold's row, decides with <see cref="HoldRules"/>, has TallyPay move the money
/// (section 16.4) and writes the new row, guarded by the status it read. No aggregate, no
/// events: the row is the truth.
/// </summary>
public static partial class HoldEndpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapHoldEndpoints()
        {
            var holds = endpoints.MapGroup("/holds").WithTags("Holds");
            holds.MapPost("", PlaceAsync);
            holds.MapPost("/{holdId}/capture", CaptureAsync);
            holds.MapPost("/{holdId}/void", VoidAsync);
            return endpoints;
        }
    }

    /// <summary>
    /// 201 with the hold once TallyPay has authorized it, or 200 with the hold placed before
    /// under the same id: a request sent again holds no more money. A refusal is 402, and no
    /// answer from TallyPay is 503: sending the same request again is then safe, because
    /// TallyPay sees the same idempotency key (section 16.4).
    /// </summary>
    public static async Task<Results<Created<HoldResponse>, Ok<HoldResponse>, ProblemHttpResult>>
        PlaceAsync(
            PlaceHoldRequest request,
            HoldStore holds,
            TallyPayClient tallyPay,
            CancellationToken cancellationToken)
    {
        if (Validate(request) is { Count: > 0 } errors)
        {
            return Problems.Invalid(errors);
        }

        if (await holds.FindAsync(request.HoldId, cancellationToken) is { } placed)
        {
            return TypedResults.Ok(placed.ToResponse());
        }

        var outcome = await tallyPay.PreauthorizeAsync(
            request.HoldId, request.DriverRef, request.Amount, request.Currency, cancellationToken);
        switch (outcome)
        {
            case Authorized authorized:
                var requested = new Hold(
                    request.HoldId,
                    request.DriverRef,
                    request.Amount,
                    request.Currency,
                    HoldStatus.Placed,
                    Captured: null,
                    authorized.Reference);
                var (hold, added) = await holds.AddAsync(requested, cancellationToken);
                return added
                    ? TypedResults.Created($"/holds/{hold.HoldId}", hold.ToResponse())
                    : TypedResults.Ok(hold.ToResponse());
            case Declined declined:
                return Problems.Declined(declined);
            case ProviderUnavailable unavailable:
                return Problems.Unavailable(unavailable);
            case null:
                throw new InvalidOperationException("The outcome is default: no case.");
        }
    }

    /// <summary>TallyPay takes the amount first; the row says so once it has.</summary>
    public static Task<Results<Ok<HoldResponse>, ProblemHttpResult>> CaptureAsync(
        string holdId,
        CapturePaymentRequest request,
        HoldStore holds,
        TallyPayClient tallyPay,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            holds,
            holdId,
            hold => HoldRules.Capture(hold, request.Amount, request.Currency),
            async hold => await tallyPay.CaptureAsync(
                    hold.HoldId,
                    ReferenceOf(hold),
                    request.Amount,
                    request.Currency,
                    cancellationToken) switch
                {
                    Authorized => await MoveAsync(
                        holds,
                        hold,
                        hold with { Status = HoldStatus.Captured, Captured = request.Amount },
                        cancellationToken),
                    Declined declined => Problems.Declined(declined),
                    ProviderUnavailable unavailable => Problems.Unavailable(unavailable),
                    null => throw new InvalidOperationException("The outcome is default."),
                },
            cancellationToken);

    /// <summary>
    /// TallyPay releases the authorization first. One it no longer has open, voided or expired
    /// on its side, has nothing left to release: the hold is voided all the same.
    /// </summary>
    public static Task<Results<Ok<HoldResponse>, ProblemHttpResult>> VoidAsync(
        string holdId,
        HoldStore holds,
        TallyPayClient tallyPay,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            holds,
            holdId,
            HoldRules.Void,
            async hold => await tallyPay.VoidAsync(
                    hold.HoldId, ReferenceOf(hold), cancellationToken) switch
                {
                    Authorized or Declined => await MoveAsync(
                        holds, hold, hold with { Status = HoldStatus.Voided }, cancellationToken),
                    ProviderUnavailable unavailable => Problems.Unavailable(unavailable),
                    null => throw new InvalidOperationException("The outcome is default."),
                },
            cancellationToken);

    /// <summary>
    /// Read, decide, ask TallyPay, write. A write that finds the row changed since it was read
    /// lost a race with another request for the same hold: it reads again and decides again,
    /// and TallyPay, asked again under the same key, does nothing twice.
    /// </summary>
    private static async Task<Results<Ok<HoldResponse>, ProblemHttpResult>> ChangeAsync(
        HoldStore holds,
        string holdId,
        Func<Hold, HoldDecision> decide,
        Func<Hold, Task<Results<Ok<HoldResponse>, ProblemHttpResult>?>> proceed,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (await holds.FindAsync(holdId, cancellationToken) is not { } hold)
            {
                return Problems.NotFound(HoldProblemCodes.UnknownHold, $"No hold {holdId}.");
            }

            var answer = decide(hold) switch
            {
                Proceed => await proceed(hold),
                AlreadyDone done => TypedResults.Ok(done.Hold.ToResponse()),
                Refuse refusal => Problems.Refused(refusal),
                null => throw new InvalidOperationException("The decision is default: no case."),
            };
            if (answer is not null)
            {
                return answer;
            }
        }
    }

    /// <summary>The new row, or null when another request changed the hold first.</summary>
    private static async Task<Results<Ok<HoldResponse>, ProblemHttpResult>?> MoveAsync(
        HoldStore holds, Hold from, Hold to, CancellationToken cancellationToken)
    {
        if (!await holds.MoveAsync(from, to, cancellationToken))
        {
            return null;
        }

        return TypedResults.Ok(to.ToResponse());
    }

    private static string ReferenceOf(Hold hold) =>
        hold.ProviderReference
            ?? throw new InvalidOperationException($"Hold {hold.HoldId} has no authorization.");

    private static Dictionary<string, string[]> Validate(PlaceHoldRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.HoldId is null || !HoldIdShape().IsMatch(request.HoldId))
        {
            errors["holdId"] = ["1 to 80 letters, digits or hyphens."];
        }

        if (string.IsNullOrWhiteSpace(request.DriverRef))
        {
            errors["driverRef"] = ["A driver reference is required."];
        }

        if (request.Amount <= 0m)
        {
            errors["amount"] = ["More than zero."];
        }

        if (request.Currency is null || !CurrencyShape().IsMatch(request.Currency))
        {
            errors["currency"] = ["An ISO 4217 code, such as USD."];
        }

        return errors;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,80}$")]
    private static partial Regex HoldIdShape();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyShape();
}
