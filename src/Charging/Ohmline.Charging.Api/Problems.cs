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
    /// <summary>
    /// The fields that are not valid, each with its message. The driver app's endpoints add a
    /// <c>code</c>, as for every problem they answer with (section 19.2).
    /// </summary>
    public static ProblemHttpResult Invalid(
        IDictionary<string, string[]> errors, string? code = null)
    {
        var problem = new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Some fields of the request are not valid.",
        };
        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return TypedResults.Problem(problem);
    }

    /// <summary>
    /// A command the app sent that names something the server does not know, such as a
    /// connector no charge point has: 422 with the code, never 404, which a client could take
    /// for a wrong URL.
    /// </summary>
    public static ProblemHttpResult Unprocessable(string code, string detail) =>
        TypedResults.Problem(
            detail,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "The command cannot be carried out.",
            extensions: new Dictionary<string, object?> { ["code"] = code });

    public static ProblemHttpResult NotFound(string detail) => TypedResults.Problem(
        detail, statusCode: StatusCodes.Status404NotFound, title: "Not found.");

    /// <summary>The aggregate's stable code travels as the <c>code</c> extension.</summary>
    public static ProblemHttpResult Rejected(CommandRejected rejected) =>
        Rejected(rejected.Code, rejected.Message);

    public static ProblemHttpResult Rejected(string code, string message) => TypedResults.Problem(
        message,
        statusCode: StatusCodes.Status409Conflict,
        title: "The command was rejected.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    public static ProblemHttpResult Timeout(TimeSpan waited) => TypedResults.Problem(
        $"No answer within {waited.TotalSeconds:0.###} s. The command may still have been " +
        "carried out: send it again with the same Idempotency-Key.",
        statusCode: StatusCodes.Status504GatewayTimeout,
        title: "The aggregate did not answer in time.");
}
