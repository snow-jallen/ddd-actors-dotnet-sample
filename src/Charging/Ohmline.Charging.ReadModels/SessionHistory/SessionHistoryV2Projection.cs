using System.Collections.Immutable;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Projections;

namespace Ohmline.Charging.ReadModels.SessionHistory;

/// <summary>
/// The session history's second version (section 20.9), built side by side with the first:
/// a projection of its own, under an id of its own, that starts at the journal's first event
/// with an offset of its own, while <see cref="SessionHistoryProjection"/> goes on serving
/// reads. Once it has caught up, <c>Ohmline:ReadModels:SessionHistoryVersion</c> switches the
/// query endpoints to its table; the first version is retired in a later release.
/// </summary>
/// <remarks>
/// It announces nothing: while both run, the first version's <see cref="SessionSummaryChanged"/>
/// is the one clients hear. Moving the announcement here is part of retiring the first.
/// </remarks>
public sealed class SessionHistoryV2Projection : IProjectionHandler
{
    /// <summary>Durable: the version is in the id, so its offsets are its own.</summary>
    public const string Id = "session-history-v2";

    public string ProjectionId => Id;

    public IReadOnlyList<string> Tags { get; } = [ChargingTags.ChargingSession];

    public ProjectionFailurePolicy FailurePolicy { get; } = new StopAfterRetries(MaxRetries: 5);

    public async Task<ImmutableArray<LiveUpdate>> HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        if (envelope.Event is not SessionEvent @event)
        {
            return [];
        }

        var row = await SessionHistoryV2Table.FindAsync(
            transaction, @event.SessionId.Value, cancellationToken);
        if (row?.Session.LastSequenceNr >= envelope.SequenceNr)
        {
            return []; // Seen it: a redelivery, or a replay from an older offset.
        }

        if (SessionHistoryV2Row.Apply(row, @event, envelope.SequenceNr) is { } next)
        {
            await SessionHistoryV2Table.UpsertAsync(transaction, next, cancellationToken);
        }

        return [];
    }
}
