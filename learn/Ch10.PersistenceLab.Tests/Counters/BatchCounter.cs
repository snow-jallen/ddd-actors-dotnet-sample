using Akka.Actor;
using Akka.Persistence;

namespace Ch10.PersistenceLab.Tests.Counters;

/// <summary>
/// PersistAll: one increment's events go to the journal as one atomic write. They are all
/// stored, or none of them is.
/// </summary>
public sealed class BatchCounter : ReceivePersistentActor
{
    private int _count;

    public BatchCounter(string persistenceId)
    {
        PersistenceId = persistenceId;
        Recover<Incremented>(_ => _count++);
        Command<GetCount>(_ => Sender.Tell(new Count(_count)));
        Command<Increment>(increment =>
        {
            var replyTo = Sender;
            PersistAll(
                Enumerable.Range(0, increment.Times).Select(_ => new Incremented()),
                _ => _count++,
                () => replyTo.Tell(new Counted(_count)));
        });
    }

    public override string PersistenceId { get; }

    public static Props Props(string persistenceId) =>
        Akka.Actor.Props.Create(() => new BatchCounter(persistenceId));
}
