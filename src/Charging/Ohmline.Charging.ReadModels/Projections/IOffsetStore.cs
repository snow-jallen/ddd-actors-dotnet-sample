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
    /// rows in, so that the rows and the offset are committed together or not at all.
    /// </summary>
    Task SaveAsync(
        ProjectionStream stream,
        long offset,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken);
}
