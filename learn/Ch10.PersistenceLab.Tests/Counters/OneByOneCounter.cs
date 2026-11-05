using Akka.Actor;
using Akka.Persistence;

namespace Ch10.PersistenceLab.Tests.Counters;

/// <summary>
/// The contrast to <see cref="BatchCounter"/>: one Persist per event, so each event is its own
/// write, and a failure part-way leaves the first ones stored. Akka.Analyzers reports this loop
/// as AK1006; build with <c>-p:DefineConstants=DEMO_COMPILE_ERRORS</c> to see it.
/// </summary>
public sealed class OneByOneCounter : ReceivePersistentActor
{
    private int _count;

    public OneByOneCounter(string persistenceId)
    {
        PersistenceId = persistenceId;
        Recover<Incremented>(_ => _count++);
        Command<GetCount>(_ => Sender.Tell(new Count(_count)));
        Command<Increment>(increment =>
        {
            for (var i = 0; i < increment.Times; i++)
            {
#if !DEMO_COMPILE_ERRORS
#pragma warning disable AK1006 // Section 10.6: the mistake that PersistAll is measured against.
#endif
                Persist(new Incremented(), _ => _count++);
#if !DEMO_COMPILE_ERRORS
#pragma warning restore AK1006
#endif
            }
        });
    }

    public override string PersistenceId { get; }

    public static Props Props(string persistenceId) =>
        Akka.Actor.Props.Create(() => new OneByOneCounter(persistenceId));
}
