using Akka.Event;
using Akka.Persistence;
using Ohmline.SharedKernel.Deciders;

namespace Ohmline.ActorShell.Deciders;

/// <summary>
/// A decider actor that keeps only its latest state: state sourcing on the journal, which is
/// how Ohmline gets what JVM Akka calls Durable State (Akka.NET ships no durable state store).
/// The decider records each change as one event that carries the whole new state; the shell
/// saves a snapshot after every event and, once the snapshot is stored, deletes the events and
/// snapshots before it. Recovery then reads one snapshot and nothing else.
/// </summary>
/// <remarks>
/// Only for aggregates whose history nobody will ever ask for: the deleted events are gone for
/// projections too. The snapshot is the only copy of the state, so its DTO and manifest must be
/// as stable as any event's. With <c>snapshot-is-optional</c> on (section 20.6), an unreadable
/// snapshot is skipped, the journal no longer starts at 1, and the shell stops the aggregate
/// rather than start it empty.
/// </remarks>
public abstract class StateSourcedDeciderActor<TDecider, TState, TCommand, TEvent, TError>
    : DeciderActor<TDecider, TState, TCommand, TEvent, TError>
    where TDecider : IDecider<TState, TCommand, TEvent, TError>
    where TCommand : class
    where TEvent : class
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    protected StateSourcedDeciderActor(string typeName, string entityId, TimeProvider clock)
        : base(typeName, entityId, clock)
    {
        Command<DeleteMessagesSuccess>(_ => { });
        Command<DeleteMessagesFailure>(failed => _log.Warning(
            failed.Cause,
            "{0} could not delete its events up to sequence number {1}; they stay until the " +
            "next snapshot",
            PersistenceId,
            failed.ToSequenceNr));
    }

    /// <summary>After every event: the snapshot is the state.</summary>
    protected sealed override int SnapshotEvery => 1;

    /// <summary>
    /// Only now, with the snapshot stored, are the older snapshots and the events it includes
    /// deleted: if the actor stops before this, recovery still has the events.
    /// </summary>
    protected override void OnSnapshotSaved(SnapshotMetadata saved)
    {
        base.OnSnapshotSaved(saved);
        DeleteMessages(saved.SequenceNr);
    }
}
