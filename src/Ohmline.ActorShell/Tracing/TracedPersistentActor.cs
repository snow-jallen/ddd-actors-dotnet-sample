using System.Diagnostics;
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
/// <para>
/// A message without an envelope records no span: untraced traffic costs what it did. With
/// nothing listening to <c>Ohmline.ActorShell</c>, a traced message costs the unwrapping and a
/// null check.
/// </para>
/// <para>
/// A persistent actor stashes the commands that arrive while it recovers or waits for a write,
/// and the stash keeps the whole envelope, so each such command passes through here twice
/// (section 17.6). The shell tells the passes apart by whether a handler ran: the first is a
/// <c>stash</c> span, and only the second counts as handled.
/// </para>
/// <para>
/// A traced command's events are stored in envelopes of their own, with the span that decided
/// them (section 17.7), so that whoever reads them later can link to it. Recovery opens them
/// before any recovery handler sees them.
/// </para>
/// </remarks>
public abstract class TracedPersistentActor : ReceivePersistentActor
{
    private readonly string _actorType;
    private object? _handled;

    protected TracedPersistentActor()
    {
        _actorType = GetType().Name;
        Recover<TraceEnvelope>(envelope => OnRecover(envelope.Message));
    }

    /// <summary>
    /// What ties this actor's traces together, such as a charging journey's id: set as
    /// <c>messaging.message.conversation_id</c> on every span it processes a message in.
    /// </summary>
    protected virtual string? ConversationId => null;

    protected override bool AroundReceive(Receive receive, object message)
    {
        var started = Stopwatch.GetTimestamp();
        var envelope = message as TraceEnvelope;
        using var span = envelope is null ? null : Start(envelope);
        _handled = null;
        try
        {
            var handled = base.AroundReceive(
                next => Invoke(receive, next), envelope?.Message ?? message);
            if (_handled is { } handledMessage)
            {
                OnMessageHandled(handledMessage, Stopwatch.GetElapsedTime(started));
            }
            else
            {
                ConsumerSpans.Stashed(span, _actorType);
            }

            return handled;
        }
        catch (Exception exception)
        {
            ConsumerSpans.Failed(span, exception);
            throw;
        }
    }

    /// <summary>
    /// The handler runs: what reaches here is what the handler gets. Almost always the message
    /// AroundReceive was given, opened already. A timer's message is the exception: Akka.NET
    /// gives AroundReceive the timer's own wrapper, and takes the message out of it only on the
    /// way to the handler, so a traced timer message (section 17.7) is opened here.
    /// </summary>
    private bool Invoke(Receive receive, object message)
    {
        if (message is not TraceEnvelope timer)
        {
            _handled = message;
            return receive(message);
        }

        _handled = timer.Message;
        using var span = Start(timer);
        try
        {
            return receive(timer.Message);
        }
        catch (Exception exception)
        {
            ConsumerSpans.Failed(span, exception);
            throw;
        }
    }

    private Activity? Start(TraceEnvelope envelope) =>
        ConsumerSpans.Start(
            _actorType, envelope, Self, ContinuesTrace(envelope.Message), ConversationId);

    /// <summary>
    /// Called once for every message a handler ran for, traced or not, with how long it took:
    /// never for a pass that only stashed it, nor for the persistence protocol's own messages.
    /// The place to count what an actor processes (Chapter 18).
    /// </summary>
    protected virtual void OnMessageHandled(object message, TimeSpan duration)
    {
    }

    /// <summary>
    /// Whether processing the message carries on its sender's trace, as a reply to a request
    /// does, or starts a trace of its own with a link to the sender's, as a timer that fires
    /// minutes later should (section 17.7). Carries on, unless a subclass says otherwise.
    /// </summary>
    protected virtual bool ContinuesTrace(object message) => true;

    /// <summary>
    /// <see cref="Eventsourced.Persist{TEvent}(TEvent, Action{TEvent})"/>, with a
    /// <c>persist</c> span for the write and an <c>apply</c> span around the callback, when the
    /// command is traced (section 17.6); the event is then stored in a trace envelope (17.7).
    /// </summary>
    protected void PersistTraced<TEvent>(TEvent @event, Action<TEvent> handler)
        where TEvent : class
    {
        var write = WriteSpans.Begin(_actorType, PersistenceId);
        Persist(TraceEnvelope.Wrap(@event), stored => Apply(write, stored, handler));
    }

    /// <summary>
    /// <see cref="Eventsourced.PersistAll{TEvent}(IEnumerable{TEvent}, Action{TEvent}, Action)"/>,
    /// with a <c>persist</c> and an <c>apply</c> span per event and a <c>persisted</c> span
    /// around <paramref name="onComplete"/>, when the command is traced (section 17.6); each
    /// event is then stored in a trace envelope (17.7).
    /// </summary>
    protected void PersistAllTraced<TEvent>(
        IEnumerable<TEvent> events, Action<TEvent> handler, Action onComplete)
        where TEvent : class
    {
        var write = WriteSpans.Begin(_actorType, PersistenceId);
        PersistAll(
            events.Select(TraceEnvelope.Wrap),
            stored => Apply(write, stored, handler),
            () =>
            {
                using var completing = write?.Completing();
                onComplete();
            });
    }

    /// <summary>The event as stored, out of its envelope, applied in its span.</summary>
    private void Apply<TEvent>(WriteSpans? write, object stored, Action<TEvent> handler)
        where TEvent : class
    {
        var @event = (TEvent)TraceEnvelope.Unwrap(stored);
        write?.Stored(@event, LastSequenceNr);
        using var applying = write?.Applying(@event);
        handler(@event);
    }
}
