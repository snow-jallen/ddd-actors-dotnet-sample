using System.Globalization;
using System.Text;

namespace Ohmline.Charging.Actors.Tests.JournalTour;

/// <summary>
/// One row of the <c>snapshot</c> table: the state of one aggregate as of one sequence number,
/// written with the serializer and manifest of its snapshot DTO. <paramref name="Created"/> is in
/// .NET ticks (UTC), like the journal's.
/// </summary>
public sealed record SnapshotRow(
    string PersistenceId,
    long SequenceNumber,
    long Created,
    string Manifest,
    int SerializerId,
    byte[] Snapshot)
{
    public DateTimeOffset CreatedAt => new(Created, TimeSpan.Zero);

    public string Json => Encoding.UTF8.GetString(Snapshot);

    public override string ToString() => string.Join(
        " | ",
        $"persistence_id={PersistenceId}",
        $"sequence_number={SequenceNumber}",
        $"created={Created} ({CreatedAt.ToString("O", CultureInfo.InvariantCulture)})",
        $"manifest={Manifest}",
        $"serializer_id={SerializerId}",
        $"snapshot={Json}");
}
