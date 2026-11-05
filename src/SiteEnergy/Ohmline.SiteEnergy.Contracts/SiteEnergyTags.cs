namespace Ohmline.SiteEnergy.Contracts;

/// <summary>
/// The tag Site Energy writes beside its events in the journal (section 13.2). Durable: events
/// already stored keep the tag they were written with, so it is never renamed.
/// </summary>
public static class SiteEnergyTags
{
    /// <summary>Site power budgets: configuration, allocations, curtailment.</summary>
    public const string SiteEnergy = "site-energy";
}
