using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Contracts;

namespace Ohmline.Charging.Api;

/// <summary>
/// Every way Charging's edge says no, as RFC 9457 problem details: what the client sent is not
/// valid (422), the resource does not exist (404), the aggregate refused the command (409), or
/// it did not answer in time (504).
/// </summary>
internal static class Problems
{
    public static ProblemHttpResult Invalid(IDictionary<string, string[]> errors) =>
        TypedResults.Problem(new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Some fields of the request are not valid.",
        });

    public static ProblemHttpResult NotFound(string detail) => TypedResults.Problem(
        detail, statusCode: StatusCodes.Status404NotFound, title: "Not found.");

    /// <summary>The aggregate's stable code travels as the <c>code</c> extension.</summary>
    public static ProblemHttpResult Rejected(CommandRejected rejected) => TypedResults.Problem(
        rejected.Message,
        statusCode: StatusCodes.Status409Conflict,
        title: "The command was rejected.",
        extensions: new Dictionary<string, object?> { ["code"] = rejected.Code });

    public static ProblemHttpResult Timeout(TimeSpan waited) => TypedResults.Problem(
        $"No answer within {waited.TotalSeconds:0.###} s. The command may still have been " +
        "carried out: send it again with the same Idempotency-Key.",
        statusCode: StatusCodes.Status504GatewayTimeout,
        title: "The aggregate did not answer in time.");
}
