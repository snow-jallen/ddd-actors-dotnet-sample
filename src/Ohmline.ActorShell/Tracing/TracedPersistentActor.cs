using Akka.Actor;
using Akka.Persistence;

namespace Ohmline.ActorShell.Tracing;

/// <summary>
/// A persistent actor that carries on the trace a message was sent in (section 17.5). Every
/// message passes through <see cref="AroundReceive"/> before any handler, so the shell opens a
/// <see cref="TraceEnvelope"/> there: it starts a CONSUMER span, <c>process {ActorType}</c>,
/// as the child of the sender's, and passes the message inside on, so that no handler ever
/// sees an envelope. While the handler runs, the span is current: what the handler logs, tells
/// or asks belongs to the same trace.
/// </summary>
/// <remarks>
/// A message without an envelope passes through untouched and records nothing: untraced
/// traffic costs what it did. With nothing listening to <c>Ohmline.ActorShell</c>, a traced
/// message costs the unwrapping and a null check.
/// </remarks>
public abstract class TracedPersistentActor : ReceivePersistentActor
{
    private readonly string _actorType;

    protected TracedPersistentActor() => _actorType = GetType().Name;

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
