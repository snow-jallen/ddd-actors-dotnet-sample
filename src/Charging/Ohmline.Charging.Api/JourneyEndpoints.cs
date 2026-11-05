using Akka.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Api;

/// <summary>Starting a charging journey, and asking where it is.</summary>
public static class JourneyEndpoints
{
    /// <summary>
    /// Asks the charge point for the connector's site and power, then starts a journey there.
    /// The journey is named after the command, so the same request sent again with the same
    /// <c>Idempotency-Key</c> finds the journey it started, and starts no other.
    /// </summary>
    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        StartAsync(
            StartChargingRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<ChargePointRegion> chargePoints,
            IRequiredActor<ChargingJourneyRegion> journeys,
            TimeProvider clock,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        var tokenUid = fields.Read("tokenUid", TokenUid.TryCreate(request.TokenUid));
        var connectorId = fields.Read("connectorId", ConnectorId.TryCreate(request.ConnectorId));
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        var timeout = options.Value.AskTimeout;
        var status = await Edge.AskAsync<ChargePointStatus>(
            chargePoints.ActorRef,
            new GetChargePointStatus(connectorId.ChargePointId),
            timeout,
            cancellationToken);
        if (status is null)
        {
            return Problems.Timeout(timeout);
        }

        var connector = status.Connectors.FirstOrDefault(
            connector => connector.ConnectorNumber == connectorId.Number);
        if (status.SiteId is not { } siteId || connector is null)
        {
            return Problems.NotFound($"Connector {connectorId} is not registered.");
        }

        var journeyId = new JourneyId(GuidV7.Derive(commandId.Value, "journey"));
        var start = new StartCharging(
            commandId,
            journeyId,
            tokenUid,
            connectorId,
            siteId,
            connector.MaxPower,
            new Money(options.Value.HoldAmount, new Currency(options.Value.HoldCurrency)));
        return await Edge.SendAsync(
            journeys.ActorRef, start, $"/journeys/{journeyId}", timeout, cancellationToken);
    }

    /// <summary>The journey as it knows itself now: never stale.</summary>
    public static async Task<Results<Ok<JourneyResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        IRequiredActor<ChargingJourneyRegion> journeys,
        IOptions<ChargingApiOptions> options,
        CancellationToken cancellationToken)
    {
        if (!JourneyId.TryCreate(id).TryGet(out var journeyId))
        {
            return Problems.NotFound($"'{id}' is not a journey id.");
        }

        var timeout = options.Value.AskTimeout;
        var view = await Edge.AskAsync<JourneyView>(
            journeys.ActorRef, new GetJourney(journeyId), timeout, cancellationToken);
        return view switch
        {
            null => Problems.Timeout(timeout),
            { Version: 0 } => Problems.NotFound($"Journey {journeyId} has not started."),
            _ => TypedResults.Ok(JourneyResponse.From(view)),
        };
    }
}
