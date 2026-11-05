using Akka.Actor;
using Akka.Persistence;

namespace Ch10.PersistenceLab.Tests.Counters;

/// <summary>
/// Persist: while the event is being written, every other message waits in the internal stash.
/// </summary>
public sealed class StashingCounter : ReceivePersistentActor
{
    private int _count;

    public StashingCounter(string persistenceId)
    {
        PersistenceId = persistenceId;
        Recover<Incremented>(_ => _count++);
        Command<GetCount>(_ => Sender.Tell(new Count(_count)));
        Command<Increment>(_ =>
        {
            var replyTo = Sender;
            Persist(new Incremented(), _ =>
            {
                _count++;
                replyTo.Tell(new Counted(_count));
            });
        });
    }

    public override string PersistenceId { get; }

    public static Props Props(string persistenceId) =>
        Akka.Actor.Props.Create(() => new StashingCounter(persistenceId));
}
