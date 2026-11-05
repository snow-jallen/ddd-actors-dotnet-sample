using Npgsql;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// Where each projection stream remembers how far it got: the journal ordering of the last
/// event it handled. Akka.Persistence.Sql's offsets are exclusive, so a stream resumes with the
/// event after the stored one.
/// </summary>
public interface IOffsetStore
{
    /// <summary>The stored offset, or 0 for a stream that has handled nothing yet.</summary>
    Task<long> LoadAsync(ProjectionStream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the offset inside <paramref name="transaction"/>, the one the projection wrote its
    /// rows in, so that the rows and the offset are committed together or not at all. With it
    /// goes <paramref name="eventWrittenAt"/>, when the event at the offset was written to the
    /// journal, so the stream's lag can be measured (section 14.3).
    /// </summary>
    Task SaveAsync(
        ProjectionStream stream,
        long offset,
        DateTimeOffset eventWrittenAt,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken);
}
