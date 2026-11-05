using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.ReadModels.Tests;

/// <summary>
/// The chargers, people and moments the read-side tests talk about: two charge points at the
/// bus depot, the primer's driver, on a weekday morning.
/// </summary>
internal static class Known
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly SiteId Depot = new("slc-depot");

    public static readonly ChargePointId Charger = new("OHM-SLC-0042");

    public static readonly ChargePointId NextCharger = new("OHM-SLC-0043");

    public static readonly TokenUid DriversCard = new("04A2B3C4D5E6F7");

    public static readonly SessionId Session = new(Guid.CreateVersion7(Morning));

    public static readonly TariffSnapshot Tariff = new(
        new TariffId("slc-standard"),
        1,
        Currency.Usd,
        [new EnergyRate(new Money(0.42m, Currency.Usd))],
        PriceCap: null,
        RoundingPolicy.Cents,
        "America/Denver");

    /// <summary>A new id on every read, as every sender gives each command its own.</summary>
    public static CommandId Command => new(Guid.CreateVersion7(Morning));

    public static RegisterChargePoint Register(ChargePointId chargePoint) =>
        new(Command, chargePoint, Depot, [new Power(22m), new Power(22m)]);

    public static ReserveConnector Reserve(ChargePointId chargePoint, int connector) =>
        new(Command, chargePoint, connector, DriversCard);

    public static StartSession Begin() => new(
        Command,
        Session,
        Depot,
        new ConnectorId(Charger, 1),
        DriversCard,
        Tariff,
        new Money(40m, Currency.Usd),
        MeterStartWh: 1_200_000);
}
