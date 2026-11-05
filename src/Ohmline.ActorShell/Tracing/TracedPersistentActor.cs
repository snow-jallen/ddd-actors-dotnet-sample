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
/// </remarks>
public abstract class TracedPersistentActor : ReceivePersistentActor
{
    private readonly string _actorType;
    private bool _handlerInvoked;

    protected TracedPersistentActor() => _actorType = GetType().Name;

    protected override bool AroundReceive(Receive receive, object message)
    {
        var started = Stopwatch.GetTimestamp();
        var envelope = message as TraceEnvelope;
        var inner = envelope?.Message ?? message;
        using var span = envelope is null ? null : ConsumerSpans.Start(_actorType, envelope, Self);
        _handlerInvoked = false;
        try
        {
            var handled = base.AroundReceive(
                next =>
                {
                    _handlerInvoked = true;
                    return receive(next);
                },
                inner);
            if (_handlerInvoked)
            {
                OnMessageHandled(inner, Stopwatch.GetElapsedTime(started));
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
    /// Called once for every message a handler ran for, traced or not, with how long it took:
    /// never for a pass that only stashed it, nor for the persistence protocol's own messages.
    /// The place to count what an actor processes (Chapter 18).
    /// </summary>
    protected virtual void OnMessageHandled(object message, TimeSpan duration)
    {
    }

    /// <summary>
    /// <see cref="Eventsourced.Persist{TEvent}(TEvent, Action{TEvent})"/>, with a
    /// <c>persist</c> span for the write and an <c>apply</c> span around the callback, when the
    /// command is traced (section 17.6).
    /// </summary>
    protected void PersistTraced<TEvent>(TEvent @event, Action<TEvent> handler)
        where TEvent : class
    {
        var write = WriteSpans.Begin(_actorType, PersistenceId);
        Persist(@event, stored => Apply(write, stored, handler));
    }

    /// <summary>
    /// <see cref="Eventsourced.PersistAll{TEvent}(IEnumerable{TEvent}, Action{TEvent}, Action)"/>,
    /// with a <c>persist</c> and an <c>apply</c> span per event and a <c>persisted</c> span
    /// around <paramref name="onComplete"/>, when the command is traced (section 17.6).
    /// </summary>
    protected void PersistAllTraced<TEvent>(
        IEnumerable<TEvent> events, Action<TEvent> handler, Action onComplete)
        where TEvent : class
    {
        var write = WriteSpans.Begin(_actorType, PersistenceId);
        PersistAll(
            events,
            stored => Apply(write, stored, handler),
            () =>
            {
                using var completing = write?.Completing();
                onComplete();
            });
    }

    private void Apply<TEvent>(WriteSpans? write, TEvent stored, Action<TEvent> handler)
        where TEvent : class
    {
        write?.Stored(stored, LastSequenceNr);
        using var applying = write?.Applying(stored);
        handler(stored);
    }
}
