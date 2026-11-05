using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.DependencyInjection;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.ActorShell.Telemetry;

/// <summary>
/// The actor shell's instruments (section 18.2): what the runtime under every context does,
/// rather than what the business does. Dead letters, actors started and stopped, how long a
/// persistent actor took to recover, and how many shards and entities each region holds.
/// </summary>
/// <remarks>
/// <para>
/// One per actor system, as an Akka.NET extension: the shell's actors and the listeners find it
/// with <see cref="For"/>, and no context's constructors change. It is made on the meter of the
/// host's <see cref="IMeterFactory"/>, found through the container Akka.Hosting gives the actor
/// system; an actor system with no container, as a test may start, gets a meter of its own.
/// </para>
/// <para>
/// The same cardinality budget as the contexts' (section 18.1): actor types, message types and
/// shard type names, all named in code. Never a path, an entity id or a persistence id.
/// </para>
/// </remarks>
public sealed class ActorShellMetrics : IExtension
{
    public const string DeadLetters = "ohmline.akka.dead_letters";
    public const string ActorLifecycle = "ohmline.akka.actor.lifecycle";
    public const string RecoveryDuration = "ohmline.akka.recovery.duration";
    public const string ShardingShards = "ohmline.akka.sharding.shards";
    public const string ShardingEntities = "ohmline.akka.sharding.entities";

    /// <summary>The actor's class name, such as <c>ChargePointActor</c>.</summary>
    public const string ActorTypeTag = "actor.type";

    /// <summary>The message's type name, as a span names it.</summary>
    public const string MessageTypeTag = "message.type";

    /// <summary><c>started</c>, <c>stopped</c> or <c>restarted</c>.</summary>
    public const string LifecycleTag = "lifecycle";

    /// <summary>The region's durable type name, such as <c>charge-point</c>.</summary>
    public const string ShardTypeTag = "sharding.type";

    /// <summary>
    /// From a snapshot and a few events (milliseconds) to a long history behind a recovery
    /// permit (seconds).
    /// </summary>
    private static readonly double[] RecoveryBoundaries =
        [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];

    private readonly ConcurrentDictionary<string, (long Shards, long Entities)> _sharding = new();
    private readonly Counter<long> _deadLetters;
    private readonly Counter<long> _lifecycle;
    private readonly Histogram<double> _recovery;

    public ActorShellMetrics(IMeterFactory meters)
        : this(ActorShellDiagnostics.CreateMeter(meters))
    {
    }

    private ActorShellMetrics(Meter meter)
    {
        _deadLetters = meter.CreateCounter<long>(
            DeadLetters, "{message}", "Messages that reached no actor");
        _lifecycle = meter.CreateCounter<long>(
            ActorLifecycle, "{actor}", "Ohmline's actors started, stopped and restarted");
        _recovery = meter.CreateHistogram(
            RecoveryDuration,
            "s",
            "From a persistent actor's start to the end of its recovery",
            tags: null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = RecoveryBoundaries });
        meter.CreateObservableGauge(
            ShardingShards,
            () => ObserveSharding(stats => stats.Shards),
            "{shard}",
            "Shards of each region, across the cluster");
        meter.CreateObservableGauge(
            ShardingEntities,
            () => ObserveSharding(stats => stats.Entities),
            "{entity}",
            "Entities alive in each region, across the cluster");
    }

    /// <summary>The actor system's shell metrics, made the first time it is asked for.</summary>
    public static ActorShellMetrics For(ActorSystem system) =>
        system.WithExtension<ActorShellMetrics, Provider>();

    public void DeadLetter(object message) =>
        _deadLetters.Add(
            1, new KeyValuePair<string, object?>(
                MessageTypeTag, MessageNames.Of(TraceEnvelope.Unwrap(message))));

    public void Lifecycle(Type actorType, string happened) =>
        _lifecycle.Add(1, new TagList
        {
            { ActorTypeTag, actorType.Name },
            { LifecycleTag, happened },
        });

    public void Recovered(Type actorType, TimeSpan took) =>
        _recovery.Record(
            took.TotalSeconds, new KeyValuePair<string, object?>(ActorTypeTag, actorType.Name));

    /// <summary>The latest figures for one region, read when the gauges are collected.</summary>
    public void Sharded(string typeName, long shards, long entities) =>
        _sharding[typeName] = (shards, entities);

    private IEnumerable<Measurement<long>> ObserveSharding(
        Func<(long Shards, long Entities), long> figure) =>
        _sharding.Select(region => new Measurement<long>(
            figure(region.Value), new KeyValuePair<string, object?>(ShardTypeTag, region.Key)));

    private sealed class Provider : ExtensionIdProvider<ActorShellMetrics>
    {
        public override ActorShellMetrics CreateExtension(ExtendedActorSystem system) =>
            system.Settings.Setup.Get<DependencyResolverSetup>() is { HasValue: true } setup
            && setup.Value.DependencyResolver.GetService<IMeterFactory>() is { } meters
                ? new ActorShellMetrics(meters)
                : new ActorShellMetrics(new Meter(ActorShellDiagnostics.MeterName));
    }
}
