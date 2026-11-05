using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Billing.Contracts.Holds;
using Ohmline.Billing.Holds;
using Ohmline.Billing.TallyPay;

namespace Ohmline.Billing;

/// <summary>
/// Every way Billing's API says no, as RFC 9457 problem details. A refusal carries one of the
/// stable <c>HoldProblemCodes</c> in its <c>code</c> extension.
/// </summary>
internal static class Problems
{
    public static ProblemHttpResult Invalid(IDictionary<string, string[]> errors) =>
        TypedResults.Problem(new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Some fields of the request are not valid.",
        });

    public static ProblemHttpResult Refused(Refuse refusal) => TypedResults.Problem(
        refusal.Message,
        statusCode: refusal.Status,
        title: "The request was refused.",
        extensions: new Dictionary<string, object?> { ["code"] = refusal.Code });

    /// <summary>402: the provider refused; its reason, in Billing's words, is the detail.</summary>
    public static ProblemHttpResult Declined(Declined declined) => Refused(new Refuse(
        StatusCodes.Status402PaymentRequired,
        HoldProblemCodes.Declined,
        $"{declined.Message} ({declined.Code})"));

    /// <summary>503: nobody knows if it happened; the same request may be sent again.</summary>
    public static ProblemHttpResult Unavailable(ProviderUnavailable unavailable) =>
        Refused(new Refuse(
            StatusCodes.Status503ServiceUnavailable,
            HoldProblemCodes.ProviderUnavailable,
            $"The payment provider did not answer: {unavailable.Reason}"));

    public static ProblemHttpResult NotFound(string code, string detail) => TypedResults.Problem(
        detail,
        statusCode: StatusCodes.Status404NotFound,
        title: "Not found.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
