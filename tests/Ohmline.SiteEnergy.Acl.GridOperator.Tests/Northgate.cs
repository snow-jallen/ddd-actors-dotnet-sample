using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Acl.GridOperator.Northgate;

namespace Ohmline.SiteEnergy.Acl.GridOperator.Tests;

/// <summary>Northgate's events about the depot, as its feed reports them.</summary>
internal static class Northgate
{
    public static readonly DateTimeOffset FivePm = new(2026, 11, 4, 17, 0, 0, TimeSpan.Zero);

    public static readonly SiteId Depot = new("slc-depot");

    public static readonly IReadOnlyDictionary<string, SiteId> Sites =
        new Dictionary<string, SiteId> { ["NG-VEN-0042"] = Depot };

    /// <summary>Draw this much less from 17:00 for two hours.</summary>
    public static DemandResponseEvent Event(
        decimal payload = -60m,
        string status = "active",
        string units = "kW",
        string id = "ng-dr-0007",
        int modification = 0,
        long sequence = 1,
        string ven = "NG-VEN-0042") =>
        new(
            id,
            modification,
            sequence,
            status,
            new Targets([ven]),
            [new Signal("LOAD_DISPATCH", "delta", units, [new Interval(FivePm, "PT2H", payload)])]);
}
