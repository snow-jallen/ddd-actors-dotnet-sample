using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Actors.Tests;

/// <summary>
/// The bus depot as the shell tests see it: a 200 kW grid connection with a 10 kW safety margin
/// and two 50 kW connectors, the first one for the fleet. Fixed values, so no test depends on the
/// wall clock.
/// </summary>
internal static class TheDepot
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly SiteId Site = new("slc-depot");

    public static readonly ConnectorId Fleet1 = new(new ChargePointId("OHM-SLC-0042"), 1);

    public static readonly ConnectorId Public2 = new(new ChargePointId("OHM-SLC-0042"), 2);

    public static readonly SessionId BusSession =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001"));

    public static readonly SessionId CarSession =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000002"));

    public static readonly CommandId Command =
        new(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042"));

    public static ConfigureSite Configure() => new(
        Command,
        Site,
        new Power(200m),
        new Power(10m),
        "America/Denver",
        [
            new ConnectorLimits(Fleet1, new Power(5m), new Power(50m), new Fleet()),
            new ConnectorLimits(Public2, new Power(5m), new Power(50m), new Public()),
        ]);

    public static RequestAllocation Request(ConnectorId connector, SessionId session) =>
        new(Command, Site, connector, session, new Power(50m));
}
