using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// The charge point, people and moments the shell tests talk about: two 22 kW connectors at the
/// bus depot, the primer's driver and someone else, on a weekday morning. Fixed values, so no
/// test depends on the wall clock.
/// </summary>
internal static class Known
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly SiteId Depot = new("slc-depot");

    public static readonly ChargePointId Charger = new("OHM-SLC-0042");

    public static readonly TokenUid DriversCard = new("04A2B3C4D5E6F7");

    public static readonly TokenUid SomeoneElsesCard = new("04FFEEDDCC");

    public static readonly SessionId Session = new(Guid.CreateVersion7(Morning));

    /// <summary>One id for every command; the shell's duplicate check is section 12.6.</summary>
    public static readonly CommandId Command = new(Guid.CreateVersion7(Morning));

    public static RegisterChargePoint Register(ChargePointId? chargePoint = null) =>
        new(Command, chargePoint ?? Charger, Depot, [new Power(22m), new Power(22m)]);

    public static ReserveConnector Reserve(int connector, TokenUid token) =>
        new(Command, Charger, connector, token);

    public static CancelReservation Cancel(int connector, TokenUid token) =>
        new(Command, Charger, connector, token);

    public static OccupyConnector Occupy(int connector, TokenUid token) =>
        new(Command, Charger, connector, Session, token);

    public static ReleaseConnector Release(int connector) => new(Command, Charger, connector);

    public static GetChargePointStatus Status(ChargePointId? chargePoint = null) =>
        new(chargePoint ?? Charger);
}
