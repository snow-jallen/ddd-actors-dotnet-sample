using Akka.Actor;
using Akka.Persistence;

namespace Ch10.PersistenceLab.Tests.Counters;

/// <summary>A meter reading, recorded as it is: nothing to decide, nothing to refuse.</summary>
public sealed record RecordReading(long RegisterWh);

public sealed record ReadingRecorded(long RegisterWh);

/// <summary>Tell the observer once every reading recorded before it is stored.</summary>
public sealed record Checkpoint(string Name);

/// <summary>
/// A high-rate recorder of facts, the case PersistAsync is for: it never rejects anything, so
/// it never needs to see its own latest state before it accepts the next reading. DeferAsync
/// runs a handler after every PersistAsync before it, without writing anything.
/// </summary>
public sealed class MeterLog : ReceivePersistentActor
{
    public MeterLog(string persistenceId, IActorRef observer)
    {
        PersistenceId = persistenceId;
        Command<RecordReading>(reading =>
            PersistAsync(new ReadingRecorded(reading.RegisterWh), recorded =>
                observer.Tell($"stored {recorded.RegisterWh} Wh")));
        Command<Checkpoint>(checkpoint =>
            DeferAsync(checkpoint, done => observer.Tell($"checkpoint {done.Name}")));
    }

    public override string PersistenceId { get; }

    public static Props Props(string persistenceId, IActorRef observer) =>
        Akka.Actor.Props.Create(() => new MeterLog(persistenceId, observer));
}
