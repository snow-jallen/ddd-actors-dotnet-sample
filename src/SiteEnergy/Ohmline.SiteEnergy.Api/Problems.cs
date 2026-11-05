using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.SiteEnergy.Contracts;

namespace Ohmline.SiteEnergy.Api;

/// <summary>Every way Site Energy's edge says no, as problem details: 422, 404, 409, 504.</summary>
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

    public static ProblemHttpResult Rejected(BudgetCommandRejected rejected) =>
        TypedResults.Problem(
            rejected.Message,
            statusCode: StatusCodes.Status409Conflict,
            title: "The command was rejected.",
            extensions: new Dictionary<string, object?> { ["code"] = rejected.Code });

    public static ProblemHttpResult Timeout(TimeSpan waited) => TypedResults.Problem(
        $"No answer within {waited.TotalSeconds:0.###} s. The command may still have been " +
        "carried out.",
        statusCode: StatusCodes.Status504GatewayTimeout,
        title: "The site's power budget did not answer in time.");
}
