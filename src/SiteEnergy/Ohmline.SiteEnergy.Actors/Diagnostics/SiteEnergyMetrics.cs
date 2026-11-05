using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Diagnostics;

/// <summary>
/// Site Energy's instruments (section 18.1), on the meter the host's
/// <see cref="IMeterFactory"/> makes. One gauge: how much of its capacity the fullest site
/// on this node has handed out, from 0 to 1, read whenever the host collects.
/// </summary>
/// <remarks>
/// <para>
/// An observable gauge: nothing is recorded as allocations change. Each budget actor leaves
/// its latest figure here, keyed by its site, and the meter reads the highest of them when it
/// is collected. The site is a key in memory and never a tag: a series per site would grow with
/// every depot Ohmline signs. Across nodes, take the highest again (<c>max</c> in PromQL).
/// </para>
/// <para>
/// A budget that passivates leaves its figure behind: its state has not changed, and is still
/// true. After a rebalance, the node that handed a site over keeps reporting the figure the site
/// had then, until that node restarts, so for a while the highest may be an old one.
/// </para>
/// <para>
/// Which site is full, and by how much, is a question for the site live board (section 13.8),
/// a read model built to answer it per site. The gauge only says that one is.
/// </para>
/// </remarks>
public sealed class SiteEnergyMetrics
{
    public const string AllocationUtilization = "ohmline.site_energy.allocation.utilization";

    private readonly ConcurrentDictionary<SiteId, double> _utilization = new();

    public SiteEnergyMetrics(IMeterFactory meters)
        : this(SiteEnergyDiagnostics.CreateMeter(meters))
    {
    }

    private SiteEnergyMetrics(Meter meter) =>
        meter.CreateObservableGauge(
            AllocationUtilization,
            ObserveUtilization,
            "1",
            "Share of its effective capacity the fullest site has allocated");

    /// <summary>
    /// For an actor that a test starts by hand, with no host: a gauge on a meter of its own,
    /// which no host's factory owns.
    /// </summary>
    public static SiteEnergyMetrics Unhosted { get; } =
        new(new Meter(SiteEnergyDiagnostics.MeterName));

    /// <summary>
    /// The site's budget as it is now. A site whose capacity is curtailed to nothing has none
    /// left to give: it counts as full.
    /// </summary>
    public void Budgeted(SiteId site, SitePowerBudgetState state)
    {
        if (state.Value is Configured configured)
        {
            var capacity = configured.EffectiveCapacity.Kilowatts;
            _utilization[site] = capacity > 0
                ? (double)(configured.Allocated.Kilowatts / capacity)
                : 1.0;
        }
    }

    private IEnumerable<Measurement<double>> ObserveUtilization() =>
        _utilization.IsEmpty ? [] : [new(_utilization.Values.Max())];
}
