using Akka.Persistence.Query;
using Npgsql;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// A projection: a read model kept up to date from the journal, one event at a time
/// (section 13.4). The runner reads the events and keeps the offset; the projection only says
/// what each event does to its tables.
/// </summary>
public interface IProjectionHandler
{
    /// <summary>
    /// Durable: the key its offsets are stored under. A new version of the read model gets a new
    /// id and starts from the first event (section 14.8).
    /// </summary>
    string ProjectionId { get; }

    /// <summary>The tags it reads. Each is a stream of its own, with its own offset.</summary>
    IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Applies one event to the read model, inside <paramref name="transaction"/>, which the
    /// runner opened and commits together with the offset. It may be called again with the same
    /// envelope, or an older one, and must then change nothing (section 13.5).
    /// </summary>
    Task HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken);
}
