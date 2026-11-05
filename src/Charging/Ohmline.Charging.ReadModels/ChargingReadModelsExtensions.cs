using Akka.Cluster.Hosting;
using Akka.Hosting;
using Npgsql;
using Ohmline.ActorShell.Clustering;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.ReadModels.DriverApp;
using Ohmline.Charging.ReadModels.Journeys;
using Ohmline.Charging.ReadModels.Policies;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.SessionHistory;
using Ohmline.Charging.ReadModels.SiteLiveBoard;

namespace Ohmline.Charging.ReadModels;

/// <summary>Charging's read side in a node, in one call.</summary>
public static class ChargingReadModelsExtensions
{
    /// <summary>
    /// Every stream of every projection, in daemon order: instance <c>i</c> of the daemon
    /// process runs stream <c>i</c>. Append only. Reordering it while any node runs would give
    /// one stream to two instances, on two nodes, for the length of a rolling update.
    /// </summary>
    public static IReadOnlyList<(IProjectionHandler Projection, string Tag)> Streams { get; } =
        [
            .. new IProjectionHandler[]
                {
                    new SessionHistoryProjection(),
                    new SiteLiveBoardProjection(),
                    new DriverAppProjection(),
                    new SessionEndedSubscriber(),
                    new JourneySessionRelay(),
                }
                .SelectMany(projection => projection.Tags.Select(tag => (projection, tag))),
        ];

    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Runs each projection stream exactly once in the cluster: as one instance of the
        /// sharded daemon process <c>projections</c>, on nodes with the <c>charging</c> role,
        /// reading the journal as often as <paramref name="options"/> says (section 14.3).
        /// The read models' PostgreSQL comes from the host's <see cref="NpgsqlDataSource"/>.
        /// </summary>
        public AkkaConfigurationBuilder AddChargingReadModels(ProjectionOptions? options = null)
        {
            akka.AddHocon((options ?? new ProjectionOptions()).ToHocon(), HoconAddMode.Prepend);
            return akka.WithShardedDaemonProcess<ProjectionDaemon>(
                ProjectionDaemon.Name,
                Streams.Count,
                (_, _, resolver) =>
                {
                    var dataSource = resolver.GetService<NpgsqlDataSource>();
                    return index => ProjectionDaemon.Props(
                        Streams[index].Projection,
                        Streams[index].Tag,
                        dataSource,
                        ProjectionRunner.DefaultBackoff);
                },
                new ClusterDaemonOptions
                {
                    Role = ClusterRoles.Charging,
                    HandoffStopMessage = Stop.Instance,
                });
        }
    }
}
