using Akka.Persistence.Journal;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Persistence;

/// <summary>Tags every event Site Energy persists with <c>site-energy</c> (section 13.2).</summary>
public sealed class SiteEnergyTagger : IWriteEventAdapter
{
    /// <summary>The adapter's name in the journal's configuration.</summary>
    public const string Name = "site-energy-tagger";

    /// <summary>The event families the journal passes through this adapter.</summary>
    public static IReadOnlyList<Type> BoundTypes { get; } = [typeof(SitePowerBudgetEvent)];

    /// <summary>No manifest of its own: the serializer's manifest is the one that counts.</summary>
    public string Manifest(object evt) => string.Empty;

    public object ToJournal(object evt) => evt switch
    {
        SitePowerBudgetEvent => new Tagged(evt, [SiteEnergyTags.SiteEnergy]),
        _ => evt,
    };
}
