using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Projections;

namespace Ohmline.Charging.ReadModels.SessionHistory;

/// <summary>
/// Every charging session as a row of <c>read_models.session_history</c>, from the
/// <c>charging-session</c> tag (section 13.5). The rules are in
/// <see cref="SessionHistoryRow.Apply"/>; this class reads the row, applies the event and
/// writes the result back, idempotently.
/// </summary>
public sealed class SessionHistoryProjection : IProjectionHandler
{
    /// <summary>Durable: its offsets are stored under it. A new table shape gets a new id.</summary>
    public const string Id = "session-history-v1";

    public string ProjectionId => Id;

    public IReadOnlyList<string> Tags { get; } = [ChargingTags.ChargingSession];

    public async Task HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        if (envelope.Event is not SessionEvent @event)
        {
            return;
        }

        var row = await SessionHistoryTable.FindAsync(
            transaction, @event.SessionId.Value, cancellationToken);
        if (row?.LastSequenceNr >= envelope.SequenceNr)
        {
            return; // Seen it: a redelivery, or a replay from an older offset.
        }

        if (SessionHistoryRow.Apply(row, @event, envelope.SequenceNr) is { } next)
        {
            await SessionHistoryTable.UpsertAsync(transaction, next, cancellationToken);
        }
    }
}
