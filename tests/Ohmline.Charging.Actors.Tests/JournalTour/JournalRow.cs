using System.Globalization;
using System.Text;

namespace Ohmline.Charging.Actors.Tests.JournalTour;

/// <summary>
/// One row of the <c>journal</c> table. <paramref name="Created"/> is the time the journal wrote
/// the row, in .NET ticks (UTC), from the database host's clock: not the time of the decision,
/// which the event itself carries as <c>occurredAt</c>.
/// </summary>
public sealed record JournalRow(
    long Ordering,
    string PersistenceId,
    long SequenceNumber,
    long Created,
    bool Deleted,
    string Manifest,
    int Identifier,
    string WriterUuid,
    byte[] Message)
{
    public DateTimeOffset CreatedAt => new(Created, TimeSpan.Zero);

    public string Json => Encoding.UTF8.GetString(Message);

    public override string ToString() => string.Join(
        " | ",
        $"ordering={Ordering}",
        $"persistence_id={PersistenceId}",
        $"sequence_number={SequenceNumber}",
        $"created={Created} ({CreatedAt.ToString("O", CultureInfo.InvariantCulture)})",
        $"deleted={Deleted}",
        $"manifest={Manifest}",
        $"identifier={Identifier}",
        $"writer_uuid={WriterUuid}",
        $"message={Json}");
}
