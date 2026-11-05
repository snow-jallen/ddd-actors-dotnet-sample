using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Domain.Tests;

/// <summary>
/// The people, places and moments the Charging tests talk about: the primer's driver at the
/// depot on a weekday morning. Fixed values, so no test depends on the wall clock.
/// </summary>
internal static class Known
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    public static readonly SiteId Depot = new("slc-depot");

    public static readonly ChargePointId Charger = new("OHM-SLC-0042");

    public static readonly TokenUid DriversCard = new("04A2B3C4D5E6F7");

    public static readonly TokenUid SomeoneElsesCard = new("04FFEEDDCC");

    public static readonly SessionId Session = new(Guid.CreateVersion7(Morning));

    public static readonly SessionId NextSession = new(Guid.CreateVersion7(Morning.AddHours(2)));

    /// <summary>
    /// The id every command in these tests carries. A decision is about one command, so one id
    /// is enough; the shell's duplicate check (section 12.6) has tests of its own.
    /// </summary>
    public static readonly CommandId Command = new(Guid.CreateVersion7(Morning));

    /// <summary>
    /// Version 3 of the depot's standard tariff as a session copies it: one energy rate at the
    /// price given, rounded to cents, with its bands (none here) in the depot's time zone.
    /// </summary>
    public static TariffSnapshot StandardTariff(decimal pricePerKwh) => new(
        new TariffId("slc-standard"),
        3,
        Currency.Usd,
        [new EnergyRate(new Money(pricePerKwh, Currency.Usd))],
        PriceCap: null,
        RoundingPolicy.Cents,
        "America/Denver");
}
