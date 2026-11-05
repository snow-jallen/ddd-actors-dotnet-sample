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
/// It makes sure the read models' tables exist, then starts the runner, and stops it when it
/// stops itself. The stream's state is all in the database: a new incarnation on another node
/// resumes from the stored offset. When the stream gives up on an event
/// (<see cref="StopAfterRetries"/>, section 13.7), the daemon stays alive and idle: were it to
/// stop, the daemon process's keep-alive would start it again within seconds, and the stop
/// would be a slow retry loop instead. What the projection says has changed goes to this node's
/// DistributedPubSub mediator, which carries it to the subscribers on every <c>charging</c>
/// node (section 14.4).
/// </remarks>
public sealed class ProjectionDaemon : ReceiveActor, IWithTimers
{
    /// <summary>Durable: the name of the sharded daemon process.</summary>
    public const string Name = "projections";

    private static readonly TimeSpan SchemaRetry = TimeSpan.FromSeconds(5);

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly ProjectionStream _stream;
    private readonly NpgsqlDataSource _dataSource;
    private readonly Func<ILoggingAdapter, ProjectionRunner> _runner;
    private RunningProjection? _running;

    public ProjectionDaemon(
        IProjectionHandler projection,
        string tag,
        NpgsqlDataSource dataSource,
        RestartSettings backoff)
    {
        _stream = new ProjectionStream(projection.ProjectionId, tag);
        _dataSource = dataSource;
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

        Receive<SchemaReady>(_ => Start());
        Receive<SchemaFailed>(failed =>
        {
            _log.Warning(
                failed.Cause, "{0} cannot reach the read models; trying again", _stream);
            Timers.StartSingleTimer(nameof(EnsureSchema), EnsureSchema.Instance, SchemaRetry);
        });
        Receive<EnsureSchema>(_ => EnsureSchemaExists());
        Receive<Stop>(_ => Context.Stop(Self));
    }

    public ITimerScheduler Timers { get; set; } = null!;

    public static Props Props(
        IProjectionHandler projection,
        string tag,
        NpgsqlDataSource dataSource,
        RestartSettings backoff) =>
        Akka.Actor.Props.Create(() => new ProjectionDaemon(projection, tag, dataSource, backoff));

    protected override void PreStart() => EnsureSchemaExists();

    protected override void PostStop() => _running?.Stop();

    private void EnsureSchemaExists() =>
        ReadModelSchema.EnsureAsync(_dataSource, CancellationToken.None).PipeTo(
            Self,
            success: () => SchemaReady.Instance,
            failure: cause => new SchemaFailed(cause));

    private void Start()
    {
        _running = _runner(_log).Start(Context.System.Materializer());
        _log.Info("{0} started on this node", _stream);
    }

    private sealed record SchemaReady
    {
        public static SchemaReady Instance { get; } = new();
    }

    private sealed record SchemaFailed(Exception Cause);

    private sealed record EnsureSchema
    {
        public static EnsureSchema Instance { get; } = new();
    }
}
