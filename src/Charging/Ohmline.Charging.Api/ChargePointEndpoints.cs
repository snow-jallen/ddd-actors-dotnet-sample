using System.Collections.Immutable;
using Akka.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Api;

/// <summary>Registering a charge point, reserving a connector, and asking for its status.</summary>
public static class ChargePointEndpoints
{
    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        RegisterAsync(
            RegisterChargePointRequest request,
            IRequiredActor<ChargePointRegion> chargePoints,
            TimeProvider clock,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        var fields = new RequestFields();
        var chargePointId =
            fields.Read("chargePointId", ChargePointId.TryCreate(request.ChargePointId));
        var siteId = fields.Read("siteId", SiteId.TryCreate(request.SiteId));
        ImmutableArray<Power> maxPowers =
        [
            .. (request.ConnectorMaxPowersKw ?? []).Select((kilowatts, index) =>
                fields.Read($"connectorMaxPowersKw[{index}]", Power.TryCreate(kilowatts))),
        ];
        if (maxPowers.IsEmpty)
        {
            fields.Invalid("connectorMaxPowersKw", "A charge point has at least one connector.");
        }

        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        return await Edge.SendAsync(
            chargePoints.ActorRef,
            new RegisterChargePoint(CommandId.New(clock), chargePointId, siteId, maxPowers),
            $"/charge-points/{chargePointId}",
            options.Value.AskTimeout,
            cancellationToken);
    }

    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        ReserveAsync(
            string id,
            int number,
            ReservationRequest request,
            IRequiredActor<ChargePointRegion> chargePoints,
            TimeProvider clock,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (!ChargePointId.TryCreate(id).TryGet(out var chargePointId))
        {
            return Problems.NotFound($"'{id}' is not a charge point id.");
        }

        var fields = new RequestFields();
        var tokenUid = fields.Read("tokenUid", TokenUid.TryCreate(request.TokenUid));
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        return await Edge.SendAsync(
            chargePoints.ActorRef,
            new ReserveConnector(CommandId.New(clock), chargePointId, number, tokenUid),
            $"/charge-points/{chargePointId}",
            options.Value.AskTimeout,
            cancellationToken);
    }

    /// <summary>The status as the charge point itself knows it now: never stale.</summary>
    public static async Task<Results<Ok<ChargePointResponse>, ProblemHttpResult>> GetAsync(
        string id,
        IRequiredActor<ChargePointRegion> chargePoints,
        IOptions<ChargingApiOptions> options,
        CancellationToken cancellationToken)
    {
        if (!ChargePointId.TryCreate(id).TryGet(out var chargePointId))
        {
            return Problems.NotFound($"'{id}' is not a charge point id.");
        }

        var timeout = options.Value.AskTimeout;
        var status = await Edge.AskAsync<ChargePointStatus>(
            chargePoints.ActorRef,
            new GetChargePointStatus(chargePointId),
            timeout,
            cancellationToken);
        return status switch
        {
            null => Problems.Timeout(timeout),
            { SiteId: { } siteId } =>
                TypedResults.Ok(ChargePointResponse.From(status, siteId.Value)),
            _ => Problems.NotFound($"Charge point {chargePointId} is not registered."),
        };
    }
}
