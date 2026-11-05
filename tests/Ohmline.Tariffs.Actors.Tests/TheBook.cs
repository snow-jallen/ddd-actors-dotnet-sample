using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Tariffs.Domain.Book;

namespace Ohmline.Tariffs.Actors.Tests;

/// <summary>The depot's tariff, as the tariff book publishes it, on a weekday morning.</summary>
internal static class TheBook
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly TariffId Standard = new("slc-standard");

    /// <summary>A fixed id, for the samples and their golden files.</summary>
    public static readonly CommandId Command =
        new(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042"));

    public static readonly TariffSnapshot Version1 = new(
        Standard,
        1,
        Currency.Usd,
        [
            new EnergyRate(new Money(0.42m, Currency.Usd)),
            new EnergyRate(
                new Money(0.29m, Currency.Usd),
                new TimeOfUseBand("night", new TimeOnly(22, 0), new TimeOnly(6, 0))),
            new SessionFee(new Money(1.00m, Currency.Usd)),
            new IdleFee(new Money(0.40m, Currency.Usd), TimeSpan.FromMinutes(15)),
        ],
        new Money(60m, Currency.Usd),
        RoundingPolicy.Cents,
        "America/Denver");

    /// <summary>A new id each time: every command its own (section 12.6).</summary>
    public static CommandId NewCommand => new(Guid.CreateVersion7(Morning));

    public static PublishTariff Publish(TariffSnapshot tariff, CommandId? id = null) => new(
        id ?? NewCommand,
        tariff.TariffId,
        tariff.Currency,
        tariff.Components,
        tariff.PriceCap,
        tariff.Rounding,
        tariff.TimeZoneId);

    public static TariffBookEvent Stamped(TariffBookEvent @event, CommandId? by = null) =>
        @event with { CommandId = by ?? NewCommand, OccurredAt = Morning };
}
