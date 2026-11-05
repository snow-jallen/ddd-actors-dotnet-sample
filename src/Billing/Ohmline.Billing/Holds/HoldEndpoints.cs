using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Billing.Contracts.Holds;

namespace Ohmline.Billing.Holds;

/// <summary>
/// The hold API (section 16.3): transaction scripts over <c>billing.holds</c>. Each request
/// reads the hold's row, decides with <see cref="HoldRules"/> and writes the new row, guarded by
/// the status it read. No aggregate, no events: the row is the truth.
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
    /// 201 with the hold, or 200 with the hold placed before under the same id: a request sent
    /// again holds no more money.
    /// </summary>
    public static async Task<Results<Created<HoldResponse>, Ok<HoldResponse>, ProblemHttpResult>>
        PlaceAsync(PlaceHoldRequest request, HoldStore holds, CancellationToken cancellationToken)
    {
        if (Validate(request) is { Count: > 0 } errors)
        {
            return Problems.Invalid(errors);
        }

        var requested = new Hold(
            request.HoldId,
            request.DriverRef,
            request.Amount,
            request.Currency,
            HoldStatus.Placed,
            Captured: null);
        var (hold, added) = await holds.AddAsync(requested, cancellationToken);
        return added
            ? TypedResults.Created($"/holds/{hold.HoldId}", hold.ToResponse())
            : TypedResults.Ok(hold.ToResponse());
    }

    public static Task<Results<Ok<HoldResponse>, ProblemHttpResult>> CaptureAsync(
        string holdId,
        CapturePaymentRequest request,
        HoldStore holds,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            holds,
            holdId,
            hold => HoldRules.Capture(hold, request.Amount, request.Currency),
            hold => hold with { Status = HoldStatus.Captured, Captured = request.Amount },
            cancellationToken);

    public static Task<Results<Ok<HoldResponse>, ProblemHttpResult>> VoidAsync(
        string holdId, HoldStore holds, CancellationToken cancellationToken) =>
        ChangeAsync(
            holds,
            holdId,
            HoldRules.Void,
            hold => hold with { Status = HoldStatus.Voided },
            cancellationToken);

    /// <summary>
    /// Read, decide, write. A write that finds the row changed since it was read lost a race
    /// with another request for the same hold; it reads again and decides again.
    /// </summary>
    private static async Task<Results<Ok<HoldResponse>, ProblemHttpResult>> ChangeAsync(
        HoldStore holds,
        string holdId,
        Func<Hold, HoldDecision> decide,
        Func<Hold, Hold> change,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (await holds.FindAsync(holdId, cancellationToken) is not { } hold)
            {
                return Problems.NotFound(HoldProblemCodes.UnknownHold, $"No hold {holdId}.");
            }

            switch (decide(hold))
            {
                case Proceed:
                    var changed = change(hold);
                    if (await holds.MoveAsync(hold, changed, cancellationToken))
                    {
                        return TypedResults.Ok(changed.ToResponse());
                    }

                    continue;
                case AlreadyDone done:
                    return TypedResults.Ok(done.Hold.ToResponse());
                case Refuse refusal:
                    return Problems.Refused(refusal);
                case null:
                    throw new InvalidOperationException("The decision is default: no case.");
            }
        }
    }

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
