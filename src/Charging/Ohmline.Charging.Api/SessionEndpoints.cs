using Akka.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Api.Tariffs;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Api;

/// <summary>Starting a session, recording its meter values, and stopping it.</summary>
public static class SessionEndpoints
{
    /// <summary>
    /// Copies the tariff's current version into the session: a later version never changes a
    /// session that has started.
    /// </summary>
    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        StartAsync(
            StartSessionRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<ChargingSessionRegion> sessions,
            [FromServices] ITariffCatalog tariffs,
            TimeProvider clock,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        var sessionId = fields.Read("sessionId", SessionId.TryCreate(request.SessionId));
        var siteId = fields.Read("siteId", SiteId.TryCreate(request.SiteId));
        var connectorId = fields.Read("connectorId", ConnectorId.TryCreate(request.ConnectorId));
        var tokenUid = fields.Read("tokenUid", TokenUid.TryCreate(request.TokenUid));
        var tariffId = fields.Read("tariffId", TariffId.TryCreate(request.TariffId));
        var tariff = fields.AreValid
            ? await tariffs.CurrentAsync(tariffId, cancellationToken)
            : null;
        if (fields.AreValid && tariff is null)
        {
            fields.Invalid("tariffId", $"No tariff {tariffId} is published.");
        }

        if (!fields.AreValid || tariff is null)
        {
            return fields.Problem();
        }

        var start = new StartSession(
            commandId,
            sessionId,
            siteId,
            connectorId,
            tokenUid,
            tariff,
            new Money(request.SpendCap, tariff.Currency),
            request.MeterStartWh);
        return await Edge.SendAsync(
            sessions.ActorRef,
            start,
            $"/sessions/{sessionId}",
            options.Value.AskTimeout,
            cancellationToken);
    }

    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        RecordMeterValueAsync(
            Guid id,
            MeterValueRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<ChargingSessionRegion> sessions,
            TimeProvider clock,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (!SessionId.TryCreate(id).TryGet(out var sessionId))
        {
            return Problems.NotFound($"'{id}' is not a session id.");
        }

        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        return await Edge.SendAsync(
            sessions.ActorRef,
            new RecordMeterValue(commandId, sessionId, request.RegisterWh, request.MeasuredAt),
            $"/sessions/{sessionId}",
            options.Value.AskTimeout,
            cancellationToken);
    }

    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        StopAsync(
            Guid id,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<ChargingSessionRegion> sessions,
            TimeProvider clock,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (!SessionId.TryCreate(id).TryGet(out var sessionId))
        {
            return Problems.NotFound($"'{id}' is not a session id.");
        }

        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        return await Edge.SendAsync(
            sessions.ActorRef,
            new StopSession(commandId, sessionId),
            $"/sessions/{sessionId}",
            options.Value.AskTimeout,
            cancellationToken);
    }
}
