using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ohmline.SiteEnergy.Actors.Diagnostics;

/// <summary>
/// Site Energy's telemetry port: one <see cref="ActivitySource"/> and one meter, both named
/// <c>Ohmline.SiteEnergy</c>, for the power budgets and the grid operator's signals.
/// </summary>
/// <remarks>
/// BCL types only, so this is a port and not a dependency: the host decides whether anything
/// listens (section 17.2), and with nothing listening a span costs a null check. No OpenTelemetry
/// package is referenced here, and the domain below never sees either.
/// </remarks>
public static class SiteEnergyDiagnostics
{
    /// <summary>Durable: dashboards, alerts and the hosts find it by this name.</summary>
    public const string Name = "Ohmline.SiteEnergy";

    /// <summary>The meter's name: the same as the source's, one of each per context.</summary>
    public const string MeterName = Name;

    /// <summary>
    /// Static, as the BCL's own sources are: <c>StartActivity</c> returns null until a listener
    /// asks for <see cref="Name"/>.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>
    /// The context's meter, made by the host's <see cref="IMeterFactory"/> rather than with
    /// <c>new</c>: the factory owns it, disposes it with the host, and lets a test watch one
    /// host's instruments without seeing another's.
    /// </summary>
    public static Meter CreateMeter(IMeterFactory meters) => meters.Create(MeterName);
}
