using Akka.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Replies;
using Ohmline.Charging.Client.Contracts.Requests;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Api.App;

/// <summary>
/// The driver app's commands under <c>/app/v1</c> (section 19.2): a thin translation from the
/// published language into Charging's commands, which go to the shard regions as any other.
/// Every request carries the command's id, made on the phone, and every answer is a 202 with a
/// <see cref="CommandAcceptedDto"/> or a problem with one of the <see cref="RejectionCodes"/>:
/// 400 or 422 for what the app sent, 409 for what the aggregate refused, 504 for no answer in
/// time. The id goes into the command as it is, so a request sent again is the same command,
/// and the aggregate answers it as it did the first time (section 19.3).
/// </summary>
public static class DriverAppCommands
{
    /// <summary>The smallest spend cap a driver may choose, in the hold's currency.</summary>
    public const decimal MinSpendCap = 1m;

    /// <summary>The largest spend cap a driver may choose, in the hold's currency.</summary>
    public const decimal MaxSpendCap = 500m;

    /// <summary>
    /// <c>POST /app/v1/journeys</c>: starts the journey the app named, holding the driver's spend
    /// cap on the card, once the charge point has said where the connector is.
    /// </summary>
    public static async Task<Results<Accepted<CommandAcceptedDto>, ProblemHttpResult>>
        StartChargingAsync(
            StartChargingRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<ChargePointRegion> chargePoints,
            IRequiredActor<ChargingJourneyRegion> journeys,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (KeyMismatch(idempotencyKey, request.CommandId) is { } mismatch)
        {
            return mismatch;
        }

        var fields = new RequestFields();
        var commandId = fields.Read("commandId", CommandId.TryCreate(request.CommandId));
        var journeyId = fields.Read("journeyId", JourneyId.TryCreate(request.JourneyId));
        var connectorId = fields.Read("connectorId", ConnectorId.TryCreate(request.ConnectorId));
        var tokenUid = fields.Read("tokenUid", TokenUid.TryCreate(request.TokenUid));
        var currency = fields.Read(
            "currency", PaidIn(options.Value.HoldCurrency, request.Currency));
        var spendCap = fields.Read("spendCapAmount", SpendCapOf(request.SpendCapAmount));

        if (!fields.AreValid)
        {
            return fields.Problem(RejectionCodes.InvalidRequest);
        }

        var timeout = options.Value.AskTimeout;
        var placement = await ConnectorPlacement.FindAsync(
            chargePoints.ActorRef, connectorId, timeout, cancellationToken);
        return placement switch
        {
            ConnectorPlaced placed => await Edge.SendForAppAsync(
                journeys.ActorRef,
                new StartCharging(
                    commandId,
                    journeyId,
                    tokenUid,
                    connectorId,
                    placed.SiteId,
                    placed.MaxPower,
                    new Money(spendCap, currency)),
                commandId,
                _ => $"/journeys/{journeyId}",
                timeout,
                cancellationToken),
            ConnectorNotRegistered => Problems.Unprocessable(
                RejectionCodes.UnknownConnector, $"Connector {connectorId} is not registered."),
            ChargePointSilent => Problems.Timeout(timeout),
            null => throw new InvalidOperationException("The placement is default."),
        };
    }

    /// <summary>
    /// <c>POST /app/v1/sessions/{id}/stop</c>: stops the session the route names.
    /// </summary>
    public static async Task<Results<Accepted<CommandAcceptedDto>, ProblemHttpResult>>
        StopChargingAsync(
            Guid id,
            StopChargingRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<ChargingSessionRegion> sessions,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (KeyMismatch(idempotencyKey, request.CommandId) is { } mismatch)
        {
            return mismatch;
        }

        var fields = new RequestFields();
        var sessionId = fields.Read("id", SessionId.TryCreate(id));
        var commandId = fields.Read("commandId", CommandId.TryCreate(request.CommandId));
        if (!fields.AreValid)
        {
            return fields.Problem(RejectionCodes.InvalidRequest);
        }

        return await Edge.SendForAppAsync(
            sessions.ActorRef,
            new StopSession(commandId, sessionId),
            commandId,
            version => SessionEndpoints.SessionLocation(sessionId, version),
            options.Value.AskTimeout,
            cancellationToken);
    }

    /// <summary>
    /// <c>POST /app/v1/charge-points/{id}/connectors/{number}/reservations</c>: holds the
    /// connector for the driver's token.
    /// </summary>
    public static async Task<Results<Accepted<CommandAcceptedDto>, ProblemHttpResult>>
        ReserveConnectorAsync(
            string id,
            int number,
            ReserveConnectorRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<ChargePointRegion> chargePoints,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (KeyMismatch(idempotencyKey, request.CommandId) is { } mismatch)
        {
            return mismatch;
        }

        var fields = new RequestFields();
        var chargePointId = fields.Read("id", ChargePointId.TryCreate(id));
        var commandId = fields.Read("commandId", CommandId.TryCreate(request.CommandId));
        var tokenUid = fields.Read("tokenUid", TokenUid.TryCreate(request.TokenUid));
        if (!fields.AreValid)
        {
            return fields.Problem(RejectionCodes.InvalidRequest);
        }

        return await Edge.SendForAppAsync(
            chargePoints.ActorRef,
            new ReserveConnector(commandId, chargePointId, number, tokenUid),
            commandId,
            _ => $"/charge-points/{chargePointId}",
            options.Value.AskTimeout,
            cancellationToken);
    }

    /// <summary>
    /// A client may send an <c>Idempotency-Key</c> header as well, as HTTP clients that retry
    /// by themselves do, but it must name the body's command: a request with two ids would be
    /// two commands when it is sent again. It is refused before anything is sent.
    /// </summary>
    private static ProblemHttpResult? KeyMismatch(string? idempotencyKey, Guid commandId) =>
        idempotencyKey is null || (Guid.TryParse(idempotencyKey, out var key) && key == commandId)
            ? null
            : Problems.BadRequest(
                RejectionCodes.IdempotencyKeyMismatch,
                $"Idempotency-Key '{idempotencyKey}' is not the body's commandId {commandId}.");

    /// <summary>The currency the app asked for, if it is the one charging is paid in.</summary>
    private static Result<Currency, ValidationError> PaidIn(string paidIn, string requested) =>
        Currency.TryCreate(requested).Bind(
            Result<Currency, ValidationError> (currency) => currency.Code == paidIn
                ? Result.Ok(currency)
                : Result.Fail<ValidationError>(new Malformed(
                    nameof(Currency), requested, $"{paidIn}, which charging is paid in")));

    /// <summary>The spend cap the driver chose, if it is within what the app may ask for.</summary>
    private static Result<decimal, ValidationError> SpendCapOf(decimal amount) =>
        amount is >= MinSpendCap and <= MaxSpendCap
            ? Result.Ok(amount)
            : Result.Fail<ValidationError>(new OutOfRange(
                "SpendCap", amount, $"from {MinSpendCap} to {MaxSpendCap}"));
}
