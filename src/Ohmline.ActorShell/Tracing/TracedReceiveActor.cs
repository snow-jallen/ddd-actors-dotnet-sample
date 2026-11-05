using Akka.Actor;

namespace Ohmline.ActorShell.Tracing;

/// <summary>
/// A <see cref="ReceiveActor"/> that carries on the trace a message was sent in (section
/// 17.5): it opens each <see cref="TraceEnvelope"/>, records a consumer span around the
/// handler, and hands the handler the message inside. Within the handler, and in whatever the
/// handler starts, the span is <see cref="System.Diagnostics.Activity.Current"/>.
/// </summary>
public abstract class TracedReceiveActor : ReceiveActor
{
    private readonly string _actorType;

    protected TracedReceiveActor() => _actorType = GetType().Name;

    protected override bool AroundReceive(Receive receive, object message)
    {
        if (message is not TraceEnvelope envelope)
        {
            return base.AroundReceive(receive, message);
        }

        using var span = ConsumerSpans.Start(_actorType, envelope, Self);
        try
        {
            return base.AroundReceive(receive, envelope.Message);
        }
        catch (Exception exception)
        {
            ConsumerSpans.Failed(span, exception);
            throw;
        }
    }
}
