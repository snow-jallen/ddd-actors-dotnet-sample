using System.Collections.Immutable;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Api.Tariffs;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;

namespace Ohmline.Charging.Api;

/// <summary>
/// The tariff book's small edge, for pricing analysts: publish a tariff's next version, and read
/// the version that sessions starting now copy.
/// </summary>
public static class TariffEndpoints
{
    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        PublishAsync(
            PublishTariffRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<TariffBookProxy> book,
            TimeProvider clock,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        var tariffId = fields.Read("tariffId", TariffId.TryCreate(request.TariffId));
        var currency = fields.Read("currency", Currency.TryCreate(request.Currency));
        ImmutableArray<TariffComponent> components = currency.Code is null
            ? [] // Prices need a valid currency; its own error is reported already.
            :
            [
                .. (request.Components ?? []).Select((component, index) =>
                    ComponentOf(fields, $"components[{index}]", component, currency)),
            ];
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        var publish = new PublishTariff(
            commandId,
            tariffId,
            currency,
            components,
            request.PriceCap is { } cap ? new Money(cap, currency) : null,
            RoundingPolicy.Cents,
            request.TimeZoneId ?? "");
        var timeout = options.Value.AskTimeout;
        try
        {
            var reply = await book.ActorRef.AskTraced<TariffBookReply>(
                publish, timeout, cancellationToken);
            return reply switch
            {
                TariffBookAccepted accepted => TypedResults.Accepted(
                    $"/tariffs/{tariffId}", new CommandAcceptedResponse(accepted.Version)),
                TariffBookRejected rejected => Problems.Rejected(rejected.Code, rejected.Message),
                CurrentTariff => throw new InvalidOperationException(
                    "The tariff book answered a command with a tariff."),
            };
        }
        catch (AskTimeoutException)
        {
            return Problems.Timeout(timeout);
        }
    }

    public static async Task<Results<Ok<TariffResponse>, ProblemHttpResult>> GetAsync(
        string id,
        IRequiredActor<TariffBookProxy> book,
        IOptions<ChargingApiOptions> options,
        CancellationToken cancellationToken)
    {
        if (!TariffId.TryCreate(id).TryGet(out var tariffId))
        {
            return Problems.NotFound($"'{id}' is not a tariff id.");
        }

        var timeout = options.Value.AskTimeout;
        var current = await Edge.AskAsync<CurrentTariff>(
            book.ActorRef, new GetTariffSnapshot(tariffId), timeout, cancellationToken);
        return current switch
        {
            null => Problems.Timeout(timeout),
            { Snapshot: { } snapshot } => TypedResults.Ok(ResponseOf(snapshot)),
            _ => Problems.NotFound($"Tariff {tariffId} is not published."),
        };
    }

    private static TariffComponent ComponentOf(
        RequestFields fields, string field, ComponentDto component, Currency currency)
    {
        var price = new Money(component.Price, currency);
        var band = component.Band is { } window
            ? new TimeOfUseBand(window.Name, window.From, window.Until)
            : null;
        switch (component.Kind)
        {
            case "energy-rate":
                return new EnergyRate(price, band);
            case "time-rate":
                return new TimeRate(price, band);
            case "session-fee":
                return new SessionFee(price);
            case "idle-fee":
                return new IdleFee(price, TimeSpan.FromMinutes(component.GraceMinutes ?? 0));
            default:
                fields.Invalid(
                    $"{field}.kind",
                    "A component is an energy-rate, a time-rate, a session-fee or an idle-fee, " +
                    $"not '{component.Kind}'.");
                return new SessionFee(price);
        }
    }

    private static TariffResponse ResponseOf(TariffSnapshot tariff) => new(
        tariff.TariffId.Value,
        tariff.Version,
        tariff.Currency.Code,
        tariff.TimeZoneId,
        [.. tariff.Components.Select(DtoOf)],
        tariff.PriceCap?.Amount);

    private static ComponentDto DtoOf(TariffComponent component) => component switch
    {
        EnergyRate rate => new("energy-rate", rate.PricePerKwh.Amount, Band: BandOf(rate.Band)),
        TimeRate rate => new("time-rate", rate.PricePerMinute.Amount, Band: BandOf(rate.Band)),
        SessionFee fee => new("session-fee", fee.Amount.Amount),
        IdleFee fee =>
            new("idle-fee", fee.PricePerMinute.Amount, (int)fee.GracePeriod.TotalMinutes),
    };

    private static BandDto? BandOf(TimeOfUseBand? band) =>
        band is null ? null : new BandDto(band.Name, band.From, band.Until);
}
