using Akka.Actor;

namespace Ohmline.ActorShell.Tracing;

/// <summary>
/// Tell and Ask that carry the current trace with the message (section 17.4). Used wherever a
/// span may be current: the HTTP edge, the ports, the projections and the actors themselves.
/// Without a current span they send the plain message, exactly as Tell and Ask do.
/// </summary>
public static class TracedMessagingExtensions
{
    extension(IActorRef recipient)
    {
        /// <summary>Tells the message, in an envelope if a span is current.</summary>
        public void TellTraced(object message, IActorRef? sender = null) =>
            recipient.Tell(TraceEnvelope.Wrap(message), sender ?? ActorRefs.NoSender);

        /// <summary>
        /// Asks with the message in a <see cref="TraceEnvelope"/> if a span is current. The
        /// reply comes back plain: the asker's span is still current where it awaits it.
        /// </summary>
        public Task<TReply> AskTraced<TReply>(
            object message,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            recipient.Ask<TReply>(TraceEnvelope.Wrap(message), timeout, cancellationToken);
    }
}
