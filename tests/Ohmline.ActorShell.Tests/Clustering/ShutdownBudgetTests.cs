using System.Text.RegularExpressions;
using Akka.Actor;
using Akka.Configuration;
using Ohmline.ActorShell.Clustering;

namespace Ohmline.ActorShell.Tests.Clustering;

/// <summary>
/// A cluster member stops inside the time Kubernetes gives it (section 21.7): every phase of
/// coordinated shutdown together fits in the host's shutdown timeout, which fits in the pods'
/// termination grace period, as the manifests in <c>deploy/k8s/base</c> set it.
/// </summary>
public sealed partial class ShutdownBudgetTests : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create(
        "budget", ConfigurationFactory.ParseString(ShutdownBudget.Hocon));

    [Fact]
    public void Every_phase_of_coordinated_shutdown_fits_in_the_host_s_shutdown_timeout()
    {
        var shutdown = CoordinatedShutdown.Get(_system);

        WorstCase(shutdown).ShouldBe(ShutdownBudget.HostShutdownTimeout);
        shutdown.Timeout(CoordinatedShutdown.PhaseServiceUnbind)
            .ShouldBe(ShutdownBudget.ServiceUnbind);
        shutdown.Timeout(CoordinatedShutdown.PhaseClusterShardingShutdownRegion)
            .ShouldBe(ShutdownBudget.ShardHandOff);
    }

    [Fact]
    public async Task Akka_s_own_defaults_would_not_fit()
    {
        var defaults = ActorSystem.Create("defaults");
        try
        {
            WorstCase(CoordinatedShutdown.Get(defaults))
                .ShouldBe(TimeSpan.FromSeconds(75));
        }
        finally
        {
            await defaults.Terminate().WaitAsync(TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("node-deployment.yaml")]
    [InlineData("gateway-deployment.yaml")]
    public void The_pods_grace_period_is_the_budget_s(string manifest)
    {
        var yaml = File.ReadAllText(Path.Combine(Root(), "deploy", "k8s", "base", manifest));

        var grace = GracePeriod().Match(yaml).Groups["seconds"].Value;

        TimeSpan.FromSeconds(int.Parse(grace)).ShouldBe(ShutdownBudget.TerminationGracePeriod);
        ShutdownBudget.HostShutdownTimeout.ShouldBeLessThan(ShutdownBudget.TerminationGracePeriod);
    }

    public void Dispose() => _system.Terminate().Wait(TimeSpan.FromSeconds(10));

    /// <summary>Akka.NET's phases, in the order they run.</summary>
    private static readonly string[] Phases =
    [
        CoordinatedShutdown.PhaseBeforeServiceUnbind,
        CoordinatedShutdown.PhaseServiceUnbind,
        CoordinatedShutdown.PhaseServiceRequestsDone,
        CoordinatedShutdown.PhaseServiceStop,
        CoordinatedShutdown.PhaseBeforeClusterShutdown,
        CoordinatedShutdown.PhaseClusterShardingShutdownRegion,
        CoordinatedShutdown.PhaseClusterLeave,
        CoordinatedShutdown.PhaseClusterExiting,
        CoordinatedShutdown.PhaseClusterExitingDone,
        CoordinatedShutdown.PhaseClusterShutdown,
        CoordinatedShutdown.PhaseBeforeActorSystemTerminate,
        CoordinatedShutdown.PhaseActorSystemTerminate,
    ];

    /// <summary>
    /// The longest coordinated shutdown can take: every phase's timeout, as if each had a task
    /// that never finished. <c>TotalTimeout</c> counts only the phases that have tasks so far.
    /// </summary>
    private static TimeSpan WorstCase(CoordinatedShutdown shutdown) =>
        Phases.Aggregate(TimeSpan.Zero, (total, phase) => total + shutdown.Timeout(phase));

    [GeneratedRegex(@"terminationGracePeriodSeconds:\s*(?<seconds>\d+)")]
    private static partial Regex GracePeriod();

    private static string Root()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory);
             folder is not null;
             folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Ohmline.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No Ohmline.slnx above {AppContext.BaseDirectory}.");
    }
}
