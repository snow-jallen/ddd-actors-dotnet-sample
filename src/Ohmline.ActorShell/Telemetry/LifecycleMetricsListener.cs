using Akka.Actor;

namespace Ohmline.ActorShell.Telemetry;

/// <summary>
/// Counts Ohmline's actors as they start, stop and restart (section 18.2), from the events
/// Akka.NET publishes when <c>akka.actor.telemetry.enabled</c> is on. Started minus stopped is
/// how many are alive; a type whose count only grows is a leak, and one that restarts is
/// failing.
/// </summary>
/// <remarks>
/// Akka.NET's own actors (journals, the cluster's daemons, the shard regions) publish the same
/// events; only types in Ohmline's namespaces are counted, so the tag's values are the classes
/// this repo names.
/// </remarks>
public sealed class LifecycleMetricsListener : ReceiveActor
{
    public LifecycleMetricsListener(ActorShellMetrics metrics) =>
        Receive<IActorTelemetryEvent>(
            happened => IsOhmlines(happened.ActorType),
            happened => metrics.Lifecycle(
                happened.ActorType,
                happened switch
                {
                    ActorStarted => "started",
                    ActorStopped => "stopped",
                    ActorRestarted => "restarted",
                    _ => "other",
                }));

    protected override void PreStart() =>
        Context.System.EventStream.Subscribe(Self, typeof(IActorTelemetryEvent));

    protected override void PostStop() => Context.System.EventStream.Unsubscribe(Self);

    private static bool IsOhmlines(Type actorType) =>
        actorType.Namespace?.StartsWith("Ohmline.", StringComparison.Ordinal) == true;
}
