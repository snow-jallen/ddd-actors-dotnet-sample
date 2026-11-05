namespace Ohmline.JournalInspect;

/// <summary>
/// One row of the journal or the snapshot table: what Akka.Persistence.Sql stored, before any
/// serializer has read it. The serializer id and the manifest say who can read the payload.
/// </summary>
/// <param name="Ordering">The journal's global position; null for a snapshot.</param>
/// <param name="Created">When the row was written: .NET ticks (UTC), the writer's clock.</param>
public sealed record StoredRow(
    long? Ordering,
    string PersistenceId,
    long SequenceNr,
    long Created,
    int? SerializerId,
    string? Manifest,
    byte[] Payload)
{
    public DateTimeOffset CreatedAt => new(Created, TimeSpan.Zero);
}
