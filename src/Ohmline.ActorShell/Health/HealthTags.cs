namespace Ohmline.ActorShell.Health;

/// <summary>
/// The tags that decide which probe runs a health check (section 18.5), written on every check
/// rather than left to each package's defaults, which differ: Akka.Hosting tags its checks
/// <c>akka</c>, <c>ready</c> or <c>liveness</c> as it sees fit.
/// </summary>
/// <remarks>
/// The same strings as <c>ServiceDefaultsExtensions.LiveTag</c> and <c>ReadyTag</c>, which map
/// <c>/health/live</c> and <c>/health/ready</c>; the service defaults know no Akka.NET, and the
/// shell knows no ASP.NET Core, so each names them, and a test checks that they agree.
/// </remarks>
public static class HealthTags
{
    /// <summary>
    /// The process can do its work at all: answering no is a reason to restart it. Never a
    /// dependency, or a database outage restarts every node at once.
    /// </summary>
    public const string Live = "live";

    /// <summary>
    /// The node can take traffic now: a member of the cluster that can reach its journal.
    /// Answering no takes it out of the load balancer until it can.
    /// </summary>
    public const string Ready = "ready";
}
