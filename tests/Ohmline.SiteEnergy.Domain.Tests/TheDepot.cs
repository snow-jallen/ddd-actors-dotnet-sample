using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Domain.Tests;

/// <summary>
/// The primer's site <c>slc-depot</c>: a 200 kW grid connection with a 10 kW safety margin, three
/// charge points with two connectors each, every connector between 5 kW and 50 kW.
/// </summary>
internal static class TheDepot
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    public static readonly SiteId Site = new("slc-depot");

    public static readonly ImmutableArray<ConnectorId> Connectors =
    [
        .. from chargePoint in new[] { "OHM-SLC-0041", "OHM-SLC-0042", "OHM-SLC-0043" }
           from number in new[] { 1, 2 }
           select new ConnectorId(new ChargePointId(chargePoint), number),
    ];

    /// <summary>One session per connector, created once: every test sees the same ids.</summary>
    public static readonly ImmutableArray<SessionId> Sessions =
    [
        .. Connectors.Index().Select(entry =>
            new SessionId(Guid.CreateVersion7(Morning.AddMinutes(entry.Index)))),
    ];

    public static readonly SessionId Latecomer = new(Guid.CreateVersion7(Morning.AddHours(1)));

    /// <summary>The id every command in these tests carries; a decision is about one.</summary>
    public static readonly CommandId Command = new(Guid.CreateVersion7(Morning));

    /// <summary>The depot's configuration; the first <paramref name="fleet"/> are fleet.</summary>
    public static ConfigureSite Configure(int fleet = 0) => new(
        Command,
        Site,
        new Power(200m),
        new Power(10m),
        "America/Denver",
        [
            .. Connectors.Index().Select(entry => new ConnectorLimits(
                entry.Item,
                new Power(5m),
                new Power(50m),
                entry.Index < fleet ? new Fleet() : new Public())),
        ]);

    /// <summary>The session on connector <paramref name="index"/> asks for power.</summary>
    public static RequestAllocation Request(int index, decimal kilowatts = 50m) =>
        new(Command, Site, Connectors[index], Sessions[index], new Power(kilowatts));

    public static ReleaseAllocation Release(int index) =>
        new(Command, Site, Connectors[index], Sessions[index]);

    extension(SitePowerBudgetEvent @event)
    {
        /// <summary>The event as a decision records it: caused by <see cref="Command"/>.</summary>
        public SitePowerBudgetEvent At(DateTimeOffset time) =>
            @event with { CommandId = Command, OccurredAt = time };
    }
}
