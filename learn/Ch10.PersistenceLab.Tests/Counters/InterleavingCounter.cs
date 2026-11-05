using Akka.Actor;
using Akka.Persistence;

namespace Ch10.PersistenceLab.Tests.Counters;

/// <summary>
/// PersistAsync: nothing waits for the write. The next message is handled at once, against a
/// count that does not include the event still being written.
/// </summary>
public sealed class InterleavingCounter : ReceivePersistentActor
{
    private int _count;

    public InterleavingCounter(string persistenceId)
    {
        PersistenceId = persistenceId;
        Recover<Incremented>(_ => _count++);
        Command<GetCount>(_ => Sender.Tell(new Count(_count)));
        Command<Increment>(_ =>
        {
            var replyTo = Sender;
            PersistAsync(new Incremented(), _ =>
            {
                _count++;
                replyTo.Tell(new Counted(_count));
            });
        });
    }

    public override string PersistenceId { get; }

    public static Props Props(string persistenceId) =>
        Akka.Actor.Props.Create(() => new InterleavingCounter(persistenceId));
}
