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

    protected override bool AroundReceive(Receive receive, object message) =>
        message is TraceEnvelope envelope
            ? Processed(envelope, inner => base.AroundReceive(receive, inner))
            // A timer's message is still in Akka.NET's timer wrapper here, and is taken out of
            // it only on the way to the handler: a traced one is opened there (section 17.7).
            : base.AroundReceive(
                next => next is TraceEnvelope timer
                    ? Processed(timer, inner => receive(inner))
                    : receive(next),
                message);

    private bool Processed(TraceEnvelope envelope, Func<object, bool> handler)
    {
        using var span = ConsumerSpans.Start(_actorType, envelope, Self);
        try
        {
            return handler(envelope.Message);
        }
        catch (Exception exception)
        {
            ConsumerSpans.Failed(span, exception);
            throw;
        }
    }
}
