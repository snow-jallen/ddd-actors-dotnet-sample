using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Billing.Holds;

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

    public static ProblemHttpResult NotFound(string code, string detail) => TypedResults.Problem(
        detail,
        statusCode: StatusCodes.Status404NotFound,
        title: "Not found.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
