using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.DeviceGateway.Ocpp.Translation;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.DeviceGateway.Ocpp.Tests;

/// <summary>
/// Frames recorded from two vendors' chargers at the depot, and the charger they talk about.
/// Voltaris follows the protocol to the letter; Chargewell reports kWh, writes tokens in lower
/// case, leaves the offset off its times, and never sends a transaction id with meter values.
/// </summary>
internal static class Recorded
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly ChargePointId Charger = new("OHM-SLC-0042");

    public static readonly TokenUid DriversCard = new("04A2B3C4D5E6F7");

    /// <summary>The session the journey named; its OCPP transaction is 220089095.</summary>
    public static readonly SessionId Session =
        new(Guid.Parse("019a4f6e-8a00-7c66-9a51-2f1c8d1e4b07"));

    public static readonly CommandId Command =
        new(Guid.Parse("019a4f6e-8a00-7000-8000-000000000001"));

    public static readonly StartingTerms Terms = new(
        new SiteId("slc-depot"),
        new TariffSnapshot(
            new TariffId("slc-standard"),
            1,
            Currency.Usd,
            [new EnergyRate(new Money(0.42m, Currency.Usd))],
            PriceCap: null,
            RoundingPolicy.Cents,
            "America/Denver"),
        new Money(50m, Currency.Usd));

    /// <summary>A charger whose journey asked it to start the session on connector 1.</summary>
    public static ChargerContext Expecting { get; } =
        (ChargerContext.For(Charger) with { Terms = Terms }).Expecting(1, Session, DriversCard);

    /// <summary>The same charger, charging the session on connector 1.</summary>
    public static ChargerContext MidSession { get; } = Expecting with
    {
        PendingStarts = Expecting.PendingStarts.Clear(),
        Transactions = Expecting.Transactions.Add(
            220089095, new OpenTransaction(220089095, 1, Session, DriversCard)),
    };

    public static string Text(string vendor, string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Frames", vendor, $"{name}.json"))
            .Trim();

    public static Call Call(string vendor, string name) =>
        OcppFrameParser.Parse(Text(vendor, name)) is Ok<OcppFrame> { Value.Value: Call call }
            ? call
            : throw new InvalidOperationException($"{vendor}/{name} is not a call.");

    public static Translation.Translation Translate(
        string vendor, string name, ChargerContext context) =>
        OcppTranslator.Translate(Call(vendor, name), context, Morning, Command);
}
