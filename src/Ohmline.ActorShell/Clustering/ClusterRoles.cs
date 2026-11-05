namespace Ohmline.ActorShell.Clustering;

/// <summary>
/// The roles a node can have in Ohmline's cluster: durable vocabulary, because shard regions
/// and singletons are placed by role, and every node of a rolling update must agree on them.
/// </summary>
public static class ClusterRoles
{
    /// <summary>The nodes that host the aggregates: <c>Ohmline.Node</c>.</summary>
    public const string Charging = "charging";

    /// <summary>
    /// The Device Gateway's nodes, which hold charger connections and no aggregates; they reach
    /// the aggregates through shard region proxies (Chapter 16).
    /// </summary>
    public const string Gateway = "gateway";
}
