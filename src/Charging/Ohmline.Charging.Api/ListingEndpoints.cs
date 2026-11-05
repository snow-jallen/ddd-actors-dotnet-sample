using Akka.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api;

/// <summary>A charge point's listing: read it, or replace it as a whole.</summary>
public static class ListingEndpoints
{
    public static async Task<Results<Ok<ListingResponse>, ProblemHttpResult>> GetAsync(
        string id,
        IRequiredActor<ChargePointListingRegion> listings,
        IOptions<ChargingApiOptions> options,
        CancellationToken cancellationToken)
    {
        if (!ChargePointId.TryCreate(id).TryGet(out var chargePointId))
        {
            return Problems.NotFound($"'{id}' is not a charge point id.");
        }

        var timeout = options.Value.AskTimeout;
        var view = await Edge.AskAsync<ChargePointListingView>(
            listings.ActorRef,
            new GetChargePointListing(chargePointId),
            timeout,
            cancellationToken);
        return view switch
        {
            null => Problems.Timeout(timeout),
            { Revision: > 0 } => TypedResults.Ok(new ListingResponse(
                view.ChargePointId.Value,
                view.Revision,
                view.DisplayName ?? "",
                view.AccessInstructions ?? "",
                view.OpeningHours ?? "")),
            _ => Problems.NotFound($"Charge point {chargePointId} has no listing yet."),
        };
    }

    /// <summary>PUT: the request is the whole listing; sending it twice changes it once.</summary>
    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        ReviseAsync(
            string id,
            ListingRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<ChargePointListingRegion> listings,
            TimeProvider clock,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (!ChargePointId.TryCreate(id).TryGet(out var chargePointId))
        {
            return Problems.NotFound($"'{id}' is not a charge point id.");
        }

        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        var revision = new ReviseChargePointListing(
            commandId,
            chargePointId,
            request.DisplayName ?? "",
            request.AccessInstructions ?? "",
            request.OpeningHours ?? "");
        return await Edge.SendAsync(
            listings.ActorRef,
            revision,
            $"/charge-points/{chargePointId}/listing",
            options.Value.AskTimeout,
            cancellationToken);
    }
}
