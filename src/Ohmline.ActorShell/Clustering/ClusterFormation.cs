using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ohmline.ActorShell.Clustering;

/// <summary>
/// How a node finds the cluster it joins, decided once from its configuration: through seed
/// nodes it is given (Aspire, section 12.10), by joining itself (a development node alone), or
/// through Akka.Management's cluster bootstrap, which asks the Kubernetes API for the other pods
/// (section 21.6). A union: in process only, and every switch over it is checked for all three.
/// </summary>
public union ClusterFormation(StaticSeedNodes, SelfJoin, KubernetesBootstrap)
{
    /// <summary>The environment variable that switches a node to Kubernetes bootstrap.</summary>
    public const string KubernetesVariable = "OHMLINE_KUBERNETES";

    /// <summary>
    /// <c>OHMLINE_KUBERNETES=true</c> wins; otherwise <c>Akka:Cluster:SeedNodes</c>. A node
    /// without seed nodes joins itself only in Development: anywhere else that is a mistake in
    /// its configuration, and it does not start.
    /// </summary>
    public static ClusterFormation From(IConfiguration configuration, IHostEnvironment environment)
    {
        if (configuration.GetValue<bool>(KubernetesVariable))
        {
            return configuration.GetSection(KubernetesBootstrap.Section).Get<KubernetesBootstrap>()
                   ?? new KubernetesBootstrap();
        }

        var seedNodes =
            configuration.GetSection("Akka:Cluster:SeedNodes").Get<string[]>() ?? [];
        return seedNodes switch
        {
            [] when environment.IsDevelopment() => new SelfJoin(),
            [] => throw new InvalidOperationException(
                "Akka:Cluster:SeedNodes is empty. Only a development node may form a " +
                $"cluster on its own; on Kubernetes, set {KubernetesVariable}=true."),
            _ => new StaticSeedNodes([.. seedNodes]),
        };
    }
}

/// <summary>Join through these addresses, the first of which joins itself.</summary>
public sealed record StaticSeedNodes(ImmutableArray<string> Addresses);

/// <summary>A development node with no seed nodes: a cluster of one.</summary>
public sealed record SelfJoin;

/// <summary>
/// Cluster bootstrap on Kubernetes, from <c>Ohmline:Kubernetes</c>. The pods that carry the
/// label <c>akka-cluster={ServiceName}</c> are the contact points; the first node forms the
/// cluster only once it has found <see cref="RequiredContactPoints"/> of them and all agree that
/// none has joined one yet.
/// </summary>
public sealed record KubernetesBootstrap
{
    public const string Section = "Ohmline:Kubernetes";

    /// <summary>The cluster's name in the pods' label, and the bootstrap's service name.</summary>
    public string ServiceName { get; init; } = "ohmline";

    /// <summary>How many pods a brand-new cluster waits for before it forms.</summary>
    public int RequiredContactPoints { get; init; } = 2;

    /// <summary>Akka.Management's HTTP port: bootstrap probes it on each contact point.</summary>
    public int ManagementPort { get; init; } = 8558;
}
