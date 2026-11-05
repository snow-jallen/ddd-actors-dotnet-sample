using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts.Live;

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

    /// <summary>What to do about an event it keeps failing on (section 13.7).</summary>
    ProjectionFailurePolicy FailurePolicy { get; }

    /// <summary>
    /// The projection as it runs in <paramref name="system"/>: one that needs to ask an actor
    /// gets what it needs here (section 14.6). Most need nothing and run as they are.
    /// </summary>
    IProjectionHandler RunningIn(ActorSystem system) => this;

    /// <summary>
    /// Applies one event to the read model, inside <paramref name="transaction"/>, which the
    /// runner opened and commits together with the offset. It may be called again with the same
    /// envelope, or an older one, and must then change nothing (section 13.5). It returns what
    /// changed, for live clients; the runner publishes that only once the transaction has
    /// committed, so a pushed version can always be read back (section 14.4).
    /// </summary>
    Task<ImmutableArray<LiveUpdate>> HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken);
}
