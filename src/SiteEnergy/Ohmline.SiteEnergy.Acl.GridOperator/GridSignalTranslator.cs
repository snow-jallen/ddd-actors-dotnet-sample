using System.Collections.Immutable;
using System.Xml;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Acl.GridOperator.Northgate;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Acl.GridOperator;

/// <summary>
/// Northgate's demand-response events in Site Energy's words (section 16.8): pure, so a table
/// of events and moments says what every site is told. An event is in force from its start for
/// its duration, unless Northgate cancelled or completed it; while it is, each site it targets
/// draws less by the signal's amount. When none is, the site draws as usual.
/// </summary>
public static class GridSignalTranslator
{
    private const string LoadDispatch = "LOAD_DISPATCH";

    /// <summary>What each targeted site should be doing at <paramref name="now"/>.</summary>
    public static ImmutableDictionary<SiteId, GridInstruction> Desired(
        IEnumerable<DemandResponseEvent> latestRevisions,
        IReadOnlyDictionary<string, SiteId> sites,
        DateTimeOffset now)
    {
        var bySite = latestRevisions
            .SelectMany(revision => revision.Targets.VenIds
                .Where(sites.ContainsKey)
                .Select(ven => (Site: sites[ven], Revision: revision)))
            .GroupBy(target => target.Site, target => target.Revision);

        return bySite.ToImmutableDictionary(
            site => site.Key,
            site => Instruction(site.Key, [.. site], now));
    }

    /// <summary>
    /// The command for an instruction. Its id is derived from the event revision, so the same
    /// instruction, sent again after a lost answer or by a poller that took over, is the same
    /// command, and the site's budget answers it as it did the first time.
    /// </summary>
    public static SitePowerBudgetCommand Command(GridInstruction instruction) =>
        instruction.Value switch
        {
            Curtail curtail => new ApplyCurtailment(
                CommandIdOf(curtail.SiteId, curtail.EventId, curtail.Modification, "curtail"),
                curtail.SiteId,
                curtail.Reduction),
            Restore restore => new EndCurtailment(
                CommandIdOf(restore.SiteId, restore.EventId, restore.Modification, "restore"),
                restore.SiteId),
            _ => throw new InvalidOperationException("The instruction is default: no case."),
        };

    /// <summary>The deepest cut in force wins; with none, the last event restores.</summary>
    private static GridInstruction Instruction(
        SiteId site, IReadOnlyList<DemandResponseEvent> revisions, DateTimeOffset now)
    {
        var inForce = revisions
            .Select(revision => (Revision: revision, Reduction: ReductionAt(revision, now)))
            .Where(cut => cut.Reduction is not null)
            .OrderByDescending(cut => cut.Reduction!.Value.Kilowatts)
            .FirstOrDefault();
        if (inForce.Revision is { } deepest)
        {
            return new Curtail(
                site, inForce.Reduction!.Value, deepest.EventId, deepest.ModificationNumber);
        }

        var latest = revisions.MaxBy(revision => revision.Sequence)!;
        return new Restore(site, latest.EventId, latest.ModificationNumber);
    }

    /// <summary>How much less to draw at the moment, if the event is in force then.</summary>
    private static Power? ReductionAt(DemandResponseEvent revision, DateTimeOffset now)
    {
        if (revision.EventStatus is "cancelled" or "completed")
        {
            return null;
        }

        var interval = revision.Signals
            .Where(signal => signal.SignalName == LoadDispatch)
            .SelectMany(signal => signal.Intervals.Select(interval => (signal.Units, interval)))
            .FirstOrDefault(entry =>
                entry.interval.Start <= now
                && now < entry.interval.Start + XmlConvert.ToTimeSpan(entry.interval.Duration));
        return interval.interval is { } current
            ? new Power(Math.Abs(Kilowatts(current.Payload, interval.Units)))
            : null;
    }

    /// <summary>Northgate writes kW, but its older systems still send W or MW.</summary>
    private static decimal Kilowatts(decimal value, string units) => units switch
    {
        "kW" => value,
        "W" => value / 1000m,
        "MW" => value * 1000m,
        _ => throw new FormatException($"A load dispatch in {units} is not a power."),
    };

    private static CommandId CommandIdOf(
        SiteId site, string eventId, int modification, string purpose) =>
        new(GuidV7.Derive(Northgate, $"{site}/{eventId}/{modification}/{purpose}"));

    /// <summary>The origin every command for Northgate's events is derived from.</summary>
    private static readonly Guid Northgate = Guid.Parse("01a1a700-0000-7000-8000-00000000a7e1");
}
