using System.Collections.Immutable;
using System.Diagnostics;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Event;
using Akka.Persistence;
using Ohmline.ActorShell.Serialization;
using Ohmline.ActorShell.Sharding;
using Ohmline.ActorShell.Telemetry;
using Ohmline.ActorShell.Tracing;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;

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
/// so the command handler registered here never takes one. A command sent with its trace is
/// processed in a span of its own (<see cref="TracedPersistentActor"/>, section 17.5).
/// </remarks>
public abstract class DeciderActor<TDecider, TState, TCommand, TEvent, TError>
    : TracedPersistentActor
    where TDecider : IDecider<TState, TCommand, TEvent, TError>
    where TCommand : class
    where TEvent : class
{
    private readonly TimeProvider _clock;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly RecentCommands _recent = new();
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private int _eventsReplayed;
    private long? _firstReplayed;
    private long _snapshotSequenceNr;
    private IActorRef? _writeFor;

    /// <param name="typeName">The durable type name, such as <c>charge-point</c>.</param>
    /// <param name="entityId">The aggregate's identity, such as <c>OHM-SLC-0042</c>.</param>
    /// <param name="clock">Read once per command, into the decision's context.</param>
    protected DeciderActor(string typeName, string entityId, TimeProvider clock)
        : this($"{typeName}-{entityId}", clock)
    {
    }

    /// <summary>For an aggregate of which there is only one, such as a cluster singleton.</summary>
    /// <param name="persistenceId">Its durable name, such as <c>tariff-book</c>.</param>
    /// <param name="clock">Read once per command, into the decision's context.</param>
    protected DeciderActor(string persistenceId, TimeProvider clock)
    {
        PersistenceId = persistenceId;
        _clock = clock;

        // Recovery: the latest snapshot, if there is one, then every later event, through Evolve
        // only. Never Decide, never a reply, never a side effect.
        Recover<SnapshotOffer>(offer =>
        {
            (State, var recent) = FromSnapshot(offer.Snapshot);
            _recent.Restore(recent);
            _snapshotSequenceNr = offer.Metadata.SequenceNr;
        });
        Recover<TEvent>(Replay);
        Recover<RecoveryCompleted>(_ => Recovered());
        Command<TCommand>(command => Handle(command, Sender));

        // From the shard: passivation, a hand-off or shutdown (section 12.4). Its turn comes
        // after everything that arrived before it, so the entity stops with nothing unanswered.
        Command<Stop>(_ => Context.Stop(Self));

        // A failed snapshot costs only replay time.
        Command<SaveSnapshotSuccess>(saved => OnSnapshotSaved(saved.Metadata));
        Command<SaveSnapshotFailure>(failed => _log.Warning(
            failed.Cause,
            "{0} could not save its snapshot at sequence number {1}; recovery replays more events",
            PersistenceId,
            failed.Metadata.SequenceNr));
        Command<DeleteSnapshotsSuccess>(_ => { });
        Command<DeleteSnapshotsFailure>(failed => _log.Warning(
            failed.Cause,
            "{0} could not delete its snapshots up to sequence number {1}",
            PersistenceId,
            failed.Criteria.MaxSequenceNr));
    }

    public override string PersistenceId { get; }

    /// <summary>
    /// Whether recovery saw the whole history: a snapshot and the events after it, or every
    /// event from sequence number 1. Not so when no snapshot was read (none was stored, or the
    /// store could not read it and skipped it, section 20.6) and the journal no longer starts at
    /// 1, because events were deleted (section 11.7).
    /// </summary>
    protected bool RecoveredWholeHistory =>
        _snapshotSequenceNr > 0 || (_firstReplayed ?? LastSequenceNr + 1) == 1;

    /// <summary>The fold of every event so far, changed only here, through Evolve.</summary>
    protected TState State { get; private set; } = TDecider.Initial;

    /// <summary>The reply to a command that was accepted, at the aggregate's new version.</summary>
    protected abstract object Accepted(long version);

    /// <summary>The reply to a command the decider refused: the error mapped to the wire.</summary>
    protected abstract object Rejected(TError error);

    /// <summary>
    /// The reply to this accepted command: <see cref="Accepted(long)"/>, unless a subclass
    /// gives some of its commands answers of their own, such as an id token's lock.
    /// </summary>
    protected virtual object Accepted(TCommand command, long version) => Accepted(version);

    /// <summary>The reply to this refused command, <see cref="Rejected(TError)"/>.</summary>
    protected virtual object Rejected(TCommand command, TError error) => Rejected(error);

    /// <summary>
    /// The reply to a command the shell could not carry out, with a code from
    /// <see cref="ShellRejectionCodes"/>, in the context's wire vocabulary.
    /// </summary>
    protected abstract object Rejected(string code, string message);

    /// <summary>
    /// A snapshot is saved after every event whose sequence number is a multiple of this: 100 by
    /// default. An aggregate chooses its own from its size and its measured replay cost; zero
    /// turns snapshots off.
    /// </summary>
    protected virtual int SnapshotEvery => 100;

    /// <summary>The id its sender gave the command (section 8.1).</summary>
    protected abstract CommandId CommandIdOf(TCommand command);

    /// <summary>The id of the command that caused the event, which every event records.</summary>
    protected abstract CommandId CausationOf(TEvent @event);

    /// <summary>
    /// The state as the aggregate's snapshot DTO, which has a manifest of its own and is never
    /// the state union itself, with the window of recent commands in it; or null when the state
    /// has nothing worth keeping.
    /// </summary>
    protected abstract object? ToSnapshot(TState state, ImmutableArray<RecentCommand> recent);

    /// <summary>The state and the recent commands back from the DTO that ToSnapshot made.</summary>
    protected abstract (TState State, ImmutableArray<RecentCommand> Recent) FromSnapshot(
        object snapshot);

    /// <summary>
    /// A snapshot is stored, which makes the older ones useless: they are deleted, so that one
    /// is kept. The events stay; a state-sourced aggregate deletes those too (section 11.7).
    /// </summary>
    protected virtual void OnSnapshotSaved(SnapshotMetadata saved) =>
        DeleteSnapshots(new SnapshotSelectionCriteria(saved.SequenceNr - 1));

    /// <summary>
    /// Called after each event of a decision is stored and applied, and after a snapshot is
    /// taken if one is due; not during recovery. Section 11.4 starts timers from here.
    /// </summary>
    protected virtual void OnEventApplied(TEvent @event)
    {
    }

    /// <summary>
    /// Called once for each command the decider decided, accepted or refused, before anything
    /// is stored or answered; never during recovery, and not for a command answered as a
    /// duplicate, which nobody decided again. The place for a context to count its commands
    /// and its refusals (section 18.1).
    /// </summary>
    protected virtual void OnDecided(
        TCommand command, Result<ImmutableArray<TEvent>, TError> decision)
    {
    }

    /// <summary>
    /// The context the decider decides this command in: the clock's reading, and nothing else
    /// unless a subclass adds to it, such as the feature flags a command depends on (section
    /// 21.8). Called for commands only, never during recovery.
    /// </summary>
    protected virtual DecisionContext ContextFor(TCommand command, DecisionContext context) =>
        context;

    /// <summary>Called once all stored events are replayed, before the first command.</summary>
    protected virtual void OnRecovered()
    {
    }

    /// <summary>
    /// For an aggregate that knows it is done: asks its shard to stop it now, rather than after
    /// the idle timeout. The shard stops routing to it, tells it <see cref="Stop"/>, holds on to
    /// whatever arrives for it meanwhile, and starts it again if anything does. Never
    /// <c>Context.Stop(Self)</c>: the shard would not know, and would keep routing messages to
    /// an actor that is gone, and the commands already waiting in its stash would be lost.
    /// </summary>
    protected void Passivate() => Context.Parent.Tell(new Passivate(Stop.Instance));

    /// <summary>
    /// The journal could not be read, or an event could not be deserialized or applied. Akka.NET
    /// stops the actor after this, and the commands that waited for the replay are lost. The next
    /// message starts it again, and if the cause is a row in the journal it fails again: a poison
    /// event. The fix is in the code (a serializer that knows the manifest, or an upcaster),
    /// never in the row (<c>docs/runbooks/poison-event.md</c>).
    /// </summary>
    /// <remarks>
    /// The sequence number is the last one recovered: the failing event comes after it, though
    /// not necessarily right after it, because Akka.Persistence.Sql reads events in batches.
    /// </remarks>
    protected override void OnRecoveryFailure(Exception reason, object? message = null)
    {
        var unknown = Causes(reason).OfType<UnknownManifestException>().FirstOrDefault();
        _log.Error(
            reason,
            "Recovery of {0} failed after sequence number {1}{2}{3}. The actor stops; see " +
            "docs/runbooks/poison-event.md",
            PersistenceId,
            LastSequenceNr,
            message is null ? "" : $" while applying {message.GetType().Name}",
            unknown is null ? "" : $", on an event with the unknown manifest '{unknown.Manifest}'");
    }

    private static IEnumerable<Exception> Causes(Exception reason)
    {
        for (Exception? cause = reason; cause is not null; cause = cause.InnerException)
        {
            yield return cause;
        }
    }

    /// <summary>
    /// Every event also says which command caused it, so the window of recent commands comes
    /// back with the state; its version is the sequence number of the command's last event.
    /// </summary>
    private void Replay(TEvent @event)
    {
        State = TDecider.Evolve(State, @event);
        _recent.Record(CausationOf(@event), LastSequenceNr);
        _firstReplayed ??= LastSequenceNr;
        _eventsReplayed++;
    }

    /// <summary>
    /// The last message of recovery. Commands that arrived since the actor started have waited
    /// in the internal stash and are handled after this, in the order they arrived. How long
    /// it took, from the actor's start, includes any wait for a recovery permit (section 11.3),
    /// and is recorded by the actor's type (section 18.2).
    /// </summary>
    /// <remarks>
    /// An aggregate that recovered only part of its history stops instead, loudly: its state
    /// would be quietly wrong. One that replayed many events because it had no snapshot to start
    /// from saves one at once, so the cache fills again without waiting for the next events.
    /// </remarks>
    private void Recovered()
    {
        if (!RecoveredWholeHistory)
        {
            _log.Error(
                "{0} cannot recover: no snapshot was read, and its events start at sequence " +
                "number {1}, not 1. It stops rather than run on part of its history; see " +
                "docs/runbooks/poison-event.md",
                PersistenceId,
                _firstReplayed ?? LastSequenceNr + 1);
            Context.Stop(Self);
            return;
        }

        var took = Stopwatch.GetElapsedTime(_startedAt);
        ActorShellMetrics.For(Context.System).Recovered(GetType(), took);
        _log.Info(
            "{0} recovered to sequence number {1} in {2} ms: {3}, {4} events replayed",
            PersistenceId,
            LastSequenceNr,
            (long)took.TotalMilliseconds,
            _snapshotSequenceNr > 0 ? $"snapshot at {_snapshotSequenceNr}" : "no snapshot",
            _eventsReplayed);
        if (_snapshotSequenceNr == 0 && SnapshotEvery > 0 && _eventsReplayed >= SnapshotEvery
            && ToSnapshot(State, _recent.ToSnapshot()) is { } snapshot)
        {
            SaveSnapshot(snapshot);
        }

        OnRecovered();
    }

    /// <summary>
    /// The command path: decide, persist the decision's events with one PersistAll, evolve the
    /// state as each is stored, and reply once the last one is. A subclass calls it for a
    /// command it gives itself, such as one from a timer, with <see cref="ActorRefs.Nobody"/>
    /// when no one waits for the reply.
    /// </summary>
    protected void Handle(TCommand command, IActorRef replyTo)
    {
        if (_recent.TryGetVersion(CommandIdOf(command), out var version))
        {
            // Sent again: a retry after a timeout, or a duplicate delivery. It gets the answer
            // it got the first time, and nothing is decided or stored again (section 12.6).
            replyTo.Tell(Accepted(command, version));
            return;
        }

        var context = ContextFor(command, DecisionContext.From(_clock));
        var decision = TDecider.Decide(State, command, context);
        OnDecided(command, decision);
        switch (decision)
        {
            case Ok<ImmutableArray<TEvent>> { Value.IsEmpty: true }:
                replyTo.Tell(Accepted(command, LastSequenceNr));
                break;
            case Ok<ImmutableArray<TEvent>> accepted:
                _writeFor = replyTo;
                PersistAllTraced(
                    accepted.Value, Apply, () => AnswerWrite(Accepted(command, LastSequenceNr)));
                break;
            case Err<TError> rejected:
                replyTo.Tell(Rejected(command, rejected.Error));
                break;
            case null:
                throw new InvalidOperationException("Decide returned a default Result.");
        }
    }

    /// <summary>
    /// The journal refused the events (for example, one could not be serialized): nothing was
    /// stored or applied, and the actor lives on. Akka.NET calls this once per event of the
    /// batch, and then still runs PersistAll's completion callback, so the sender is answered
    /// here, once, and the completion finds nobody left to tell that it was accepted.
    /// </summary>
    protected override void OnPersistRejected(Exception cause, object @event, long sequenceNr)
    {
        _log.Error(
            cause,
            "{0} could not persist {1} with sequence number {2}: the journal rejected it. " +
            "Nothing changed, and the sender is told {3}",
            PersistenceId,
            MessageNames.Of(TraceEnvelope.Unwrap(@event)),
            sequenceNr,
            ShellRejectionCodes.PersistenceRejected);
        AnswerWrite(Rejected(
            ShellRejectionCodes.PersistenceRejected,
            $"The decision could not be stored ({cause.Message}). Nothing changed."));
    }

    /// <summary>
    /// The journal failed the write, and Akka.NET stops the actor after this: it no longer
    /// knows what is stored. The sender gets no answer, and neither do the commands stashed
    /// behind the write, which become dead letters. Their senders time out and retry with the
    /// same command id (section 12.6), and a supervisor or the shard starts a new incarnation
    /// that recovers whatever was stored.
    /// </summary>
    protected override void OnPersistFailure(Exception cause, object @event, long sequenceNr)
    {
        _log.Error(
            cause,
            "Failed to persist {0} with sequence number {1} for {2}. The actor stops; the " +
            "commands waiting behind the write become dead letters",
            MessageNames.Of(TraceEnvelope.Unwrap(@event)),
            sequenceNr,
            PersistenceId);
    }

    private void AnswerWrite(object reply)
    {
        _writeFor?.Tell(reply);
        _writeFor = null;
    }

    private void Apply(TEvent @event)
    {
        State = TDecider.Evolve(State, @event);
        _recent.Record(CausationOf(@event), LastSequenceNr);
        if (SnapshotEvery > 0 && LastSequenceNr % SnapshotEvery == 0
            && ToSnapshot(State, _recent.ToSnapshot()) is { } snapshot)
        {
            // Asynchronous: the next command does not wait for the snapshot store.
            SaveSnapshot(snapshot);
        }

        OnEventApplied(@event);
    }
}
