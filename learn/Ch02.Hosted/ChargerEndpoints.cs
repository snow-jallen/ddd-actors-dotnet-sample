using Akka.Actor;
using Akka.Hosting;
using Ch02.AkkaBasics.Tests.Chargers;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ch02.Hosted;

public static class ChargerEndpoints
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(2);

    public static IEndpointRouteBuilder MapChargerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/chargers/{id}/status", GetStatusAsync);
        endpoints.MapPost("/chargers/{id}/boot-notification", BootAsync);
        return endpoints;
    }

    private static async Task<Results<Ok<ChargerStatusReport>, NotFound, ProblemHttpResult>>
        GetStatusAsync(string id, IRequiredActor<ChargerTwin> twin, CancellationToken ct)
    {
        if (id != ChargerApp.ChargePointId)
            return TypedResults.NotFound();

        var twinRef = await twin.GetAsync(ct);
        try
        {
            var report = await twinRef.Ask<ChargerStatusReport>(new GetStatus(), AskTimeout, ct);
            return TypedResults.Ok(report);
        }
        catch (AskTimeoutException)
        {
            // A twin that has not booted stashes the question, so the ask runs out of time.
            return TypedResults.Problem(
                "The charger twin did not answer in time.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    // Stands in for the charger itself until the Device Gateway arrives (Chapter 16).
    private static async Task<Results<Accepted, NotFound>> BootAsync(
        string id, BootNotification boot, IRequiredActor<ChargerTwin> twin, CancellationToken ct)
    {
        if (id != ChargerApp.ChargePointId)
            return TypedResults.NotFound();

        (await twin.GetAsync(ct)).Tell(boot);
        return TypedResults.Accepted($"/chargers/{id}/status");
    }
}
