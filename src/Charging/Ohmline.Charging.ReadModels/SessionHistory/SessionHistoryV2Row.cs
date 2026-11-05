using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.ReadModels.SessionHistory;

/// <summary>
/// One row of <c>read_models.session_history_v2</c> (section 20.9): the first version's row,
/// by the same rules, plus the session's site and the phases the car charged on, which the
/// first table never had. A new table with a new projection id, built from the journal beside
/// the old one, because a read model is changed by rebuilding it, never by migrating its rows.
/// </summary>
/// <param name="PhaseCount">Null for a session started before section 20.2 recorded it.</param>
public sealed record SessionHistoryV2Row(
    SessionHistoryRow Session, string SiteId, int? PhaseCount)
{
    /// <summary>The row after one more of the session's events; null as long as v1's is.</summary>
    public static SessionHistoryV2Row? Apply(
        SessionHistoryV2Row? row, SessionEvent @event, long sequenceNr) =>
        (row, SessionHistoryRow.Apply(row?.Session, @event, sequenceNr), @event) switch
        {
            (_, null, _) => null,
            (null, { } session, SessionStarted started) =>
                new SessionHistoryV2Row(session, started.SiteId.Value, started.PhaseCount),
            ({ } existing, { } session, _) => existing with { Session = session },
            (null, { }, _) => throw new InvalidOperationException(
                "The first version made a row from an event that is not the session's start."),
        };
}
