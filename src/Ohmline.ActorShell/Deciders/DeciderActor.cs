using System.Collections.Immutable;
using System.Diagnostics;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;

namespace Ohmline.ActorShell.Deciders;

/// <summary>
/// The actor shell around any decider, written once (section 10.10). It owns what a decider must
/// not: identity (the persistence id), concurrency (one message at a time), durability (the
/// journal), time (the clock, read once per command) and messaging (the reply). The command
/// path is section 10.4's: capture the sender, decide, persist the decision's events with one
/// PersistAll, evolve the state in each callback and reply once the last one is stored. A
/// rejection persists nothing and is answered at once.
/// </summary>
/// <remarks>
/// A subclass supplies the replies, in its context's own wire vocabulary, and may answer
/// queries. Queries are a message family of their own, never a <typeparamref name="TCommand"/>,
/// so the command handler registered here never takes one.
/// </remarks>
public abstract class DeciderActor<TDecider, TState, TCommand, TEvent, TError>
    : ReceivePersistentActor
    where TDecider : IDecider<TState, TCommand, TEvent, TError>
    where TCommand : class
    where TEvent : class
{
    private readonly TimeProvider _clock;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private int _eventsReplayed;

    /// <param name="typeName">The durable type name, such as <c>charge-point</c>.</param>
    /// <param name="entityId">The aggregate's identity, such as <c>OHM-SLC-0042</c>.</param>
    /// <param name="clock">Read once per command, into the decision's context.</param>
    protected DeciderActor(string typeName, string entityId, TimeProvider clock)
    {
        PersistenceId = $"{typeName}-{entityId}";
        _clock = clock;

        // Recovery: replay through Evolve only. Never Decide, never a reply, never a side effect.
        Recover<TEvent>(Replay);
        Recover<RecoveryCompleted>(_ => Recovered());
        Command<TCommand>(Handle);
    }

    public override string PersistenceId { get; }

    /// <summary>The fold of every event so far, changed only here, through Evolve.</summary>
    protected TState State { get; private set; } = TDecider.Initial;

    /// <summary>The reply to a command that was accepted, at the aggregate's new version.</summary>
    protected abstract object Accepted(long version);

    /// <summary>The reply to a command the decider refused: the error mapped to the wire.</summary>
    protected abstract object Rejected(TError error);

    /// <summary>
    /// Called after each event of a decision is stored and applied, not during recovery. Chapter
    /// 11 saves snapshots and starts timers from here.
    /// </summary>
    protected virtual void OnEventApplied(TEvent @event)
    {
    }

    /// <summary>Called once all stored events are replayed, before the first command.</summary>
    protected virtual void OnRecovered()
    {
    }

    private void Replay(TEvent @event)
    {
        State = TDecider.Evolve(State, @event);
        _eventsReplayed++;
    }

    /// <summary>
    /// The last message of recovery. Commands that arrived since the actor started have waited
    /// in the internal stash and are handled after this, in the order they arrived.
    /// </summary>
    private void Recovered()
    {
        _log.Info(
            "{0} recovered to sequence number {1} in {2} ms: {3} events replayed",
            PersistenceId,
            LastSequenceNr,
            (long)Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds,
            _eventsReplayed);
        OnRecovered();
    }

    private void Handle(TCommand command)
    {
        var replyTo = Sender;
        switch (TDecider.Decide(State, command, DecisionContext.From(_clock)))
        {
            case Ok<ImmutableArray<TEvent>> { Value.IsEmpty: true }:
                replyTo.Tell(Accepted(LastSequenceNr));
                break;
            case Ok<ImmutableArray<TEvent>> accepted:
                PersistAll(accepted.Value, Apply, () => replyTo.Tell(Accepted(LastSequenceNr)));
                break;
            case Err<TError> rejected:
                replyTo.Tell(Rejected(rejected.Error));
                break;
            case null:
                throw new InvalidOperationException("Decide returned a default Result.");
        }
    }

    private void Apply(TEvent @event)
    {
        State = TDecider.Evolve(State, @event);
        OnEventApplied(@event);
    }
}
