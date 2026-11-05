namespace Ohmline.Charging.Actors.Tests.JournalTour;

/// <summary>
/// One row of the <c>tags</c> table (section 13.2): an event's tag, and where the event is. The
/// ordering id is the event's <c>ordering</c> in the journal, the offset that <c>EventsByTag</c>
/// reports for it.
/// </summary>
public sealed record TagRow(long OrderingId, string Tag, string PersistenceId, long SequenceNr)
{
    public override string ToString() =>
        $"ordering_id={OrderingId} | tag={Tag} | persistence_id={PersistenceId} | " +
        $"sequence_nr={SequenceNr}";
}
