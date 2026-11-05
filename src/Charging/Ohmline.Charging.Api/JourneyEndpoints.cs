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
        var journeyId = new JourneyId(GuidV7.Derive(commandId.Value, "journey"));
        var hold = new Money(options.Value.HoldAmount, new Currency(options.Value.HoldCurrency));
        var placement = await ConnectorPlacement.FindAsync(
            chargePoints.ActorRef, connectorId, timeout, cancellationToken);
        return placement switch
        {
            ConnectorPlaced placed => await Edge.SendAsync(
                journeys.ActorRef,
                new StartCharging(
                    commandId,
                    journeyId,
                    tokenUid,
                    connectorId,
                    placed.SiteId,
                    placed.MaxPower,
                    hold),
                $"/journeys/{journeyId}",
                timeout,
                cancellationToken),
            ConnectorNotRegistered =>
                Problems.NotFound($"Connector {connectorId} is not registered."),
            ChargePointSilent => Problems.Timeout(timeout),
            null => throw new InvalidOperationException("The placement is default."),
        };
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
