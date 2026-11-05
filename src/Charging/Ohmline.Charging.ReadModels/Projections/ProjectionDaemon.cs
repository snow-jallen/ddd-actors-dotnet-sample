using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Event;
using Akka.Streams;
using Npgsql;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.ReadModels.Query;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// The one place in the cluster where a projection stream runs (section 13.6): an instance of
/// the sharded daemon process <c>projections</c>. Cluster sharding keeps each instance on
/// exactly one node with the <c>charging</c> role, and moves it when that node leaves.
/// </summary>
/// <remarks>
/// It starts the runner, and stops it when it stops itself; the read models' tables are the
/// migrator's, made before any node starts (section 20.8), and a database that cannot be reached
/// is the runner's backoff to wait out. The stream's state is all in the database: a new
/// incarnation on another node
/// resumes from the stored offset. When the stream gives up on an event
/// (<see cref="StopAfterRetries"/>, section 13.7), the daemon stays alive and idle: were it to
/// stop, the daemon process's keep-alive would start it again within seconds, and the stop
/// would be a slow retry loop instead. What the projection says has changed goes to this node's
/// DistributedPubSub mediator, which carries it to the subscribers on every <c>charging</c>
/// node (section 14.4).
/// </remarks>
public sealed class ProjectionDaemon : ReceiveActor
{
    /// <summary>Durable: the name of the sharded daemon process.</summary>
    public const string Name = "projections";

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly ProjectionStream _stream;
    private readonly Func<ILoggingAdapter, ProjectionRunner> _runner;
    private RunningProjection? _running;

    public ProjectionDaemon(
        IProjectionHandler projection,
        string tag,
        NpgsqlDataSource dataSource,
        RestartSettings backoff)
    {
        _stream = new ProjectionStream(projection.ProjectionId, tag);
        var mediator = DistributedPubSub.Get(Context.System).Mediator;
        _runner = log => new ProjectionRunner(
            Context.System.ReadJournal,
            dataSource,
            new NpgsqlOffsetStore(dataSource),
            projection.RunningIn(Context.System),
            tag,
            backoff,
            change => mediator.Tell(new Publish(LiveTopics.Of(change), change)),
            log);

        Receive<Stop>(_ => Context.Stop(Self));
    }

    public static Props Props(
        IProjectionHandler projection,
        string tag,
        NpgsqlDataSource dataSource,
        RestartSettings backoff) =>
        Akka.Actor.Props.Create(() => new ProjectionDaemon(projection, tag, dataSource, backoff));

    protected override void PreStart()
    {
        _running = _runner(_log).Start(Context.System.Materializer());
        _log.Info("{0} started on this node", _stream);
    }

    protected override void PostStop() => _running?.Stop();
}
