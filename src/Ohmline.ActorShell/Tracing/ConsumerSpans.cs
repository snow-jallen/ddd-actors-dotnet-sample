using System.Diagnostics;
using Akka.Actor;
using Ohmline.ActorShell.Diagnostics;

namespace Ohmline.ActorShell.Tracing;

/// <summary>
/// The span an actor records while it processes a traced message (section 17.5), named and
/// tagged as OpenTelemetry's messaging conventions ask: a CONSUMER span called
/// <c>process {ActorType}</c>, the child of the sender's span.
/// </summary>
internal static class ConsumerSpans
{
    /// <summary>Not a registered messaging system: the conventions allow one of your own.</summary>
    public const string MessagingSystem = "akka";

    /// <summary>
    /// Starts the span, or returns null when nothing listens to <c>Ohmline.ActorShell</c>. The
    /// actor type names the destination: low cardinality, unlike the path, which is a span
    /// attribute only and never a metric tag.
    /// </summary>
    public static Activity? Start(string actorType, TraceEnvelope envelope, IActorRef self)
    {
        var span = ActorShellDiagnostics.ActivitySource.StartActivity(
            $"process {actorType}", ActivityKind.Consumer, envelope.ParentContext);
        if (span is { IsAllDataRequested: true })
        {
            span.SetTag("messaging.system.name", MessagingSystem);
            span.SetTag("messaging.operation.type", "process");
            span.SetTag("messaging.operation.name", "process");
            span.SetTag("messaging.destination.name", actorType);
            span.SetTag("akka.message.type", MessageNames.Of(envelope.Message));
            span.SetTag("akka.actor.path", self.Path.ToString());
        }

        return span;
    }

    /// <summary>The handler threw: the span says so, and the actor's supervisor decides.</summary>
    public static void Failed(Activity? span, Exception exception)
    {
        span?.AddException(exception);
        span?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
}
