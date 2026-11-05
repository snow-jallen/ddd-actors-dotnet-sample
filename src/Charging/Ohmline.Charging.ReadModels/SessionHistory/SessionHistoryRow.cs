using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.ReadModels.SessionHistory;

/// <summary>
/// One row of <c>read_models.session_history</c>: a session as a driver's history shows it.
/// Plain column values, because this is the table's shape, not the domain's.
/// <paramref name="LastSequenceNr"/> is the session's version when the row was written: the
/// sequence number of the last event applied, which guards the upsert and, from section 14.1,
/// answers "has the read model caught up with version n yet?".
/// </summary>
public sealed record SessionHistoryRow(
    Guid SessionId,
    string TokenUid,
    string ConnectorId,
    string Status,
    decimal EnergyKwh,
    long MeterStartWh,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long LastSequenceNr)
{
    /// <summary>
    /// The row after one more of the session's events, numbered <paramref name="sequenceNr"/>
    /// in its journal: a pure function, so every rule of the read model is tested without a
    /// database. Null while there is no row and the event is not the session's start.
    /// </summary>
    public static SessionHistoryRow? Apply(
        SessionHistoryRow? row, SessionEvent @event, long sequenceNr)
    {
        var next = (row, @event) switch
        {
            (null, SessionStarted started) => new SessionHistoryRow(
                started.SessionId.Value,
                started.TokenUid.Value,
                started.ConnectorId.ToString(),
                SessionHistoryStatus.Charging,
                EnergyKwh: 0m,
                started.MeterStartWh,
                started.OccurredAt,
                EndedAt: null,
                sequenceNr),
            (null, _) => null,
            (_, MeterValueRecorded { Phase: null } recorded) => row with
            {
                EnergyKwh = DeliveredSince(row.MeterStartWh, recorded.Register).KilowattHours,
            },
            (_, ChargingSuspendedByEv) => row with { Status = SessionHistoryStatus.SuspendedByEv },
            (_, ChargingSuspendedByCharger) =>
                row with { Status = SessionHistoryStatus.SuspendedByCharger },
            (_, ChargingResumed) => row with { Status = SessionHistoryStatus.Charging },
            (_, SessionEnded ended) => row with
            {
                Status = SessionHistoryStatus.Ended,
                EnergyKwh = ended.EnergyDelivered.KilowattHours,
                EndedAt = ended.OccurredAt,
            },
            (_, SessionStarted or MeterValueRecorded or MeterAnomalyDetected or SpendCapReached) =>
                row,
        };

        return next is null ? null : next with { LastSequenceNr = sequenceNr };
    }

    private static Energy DeliveredSince(long startWh, Energy register) =>
        register - Energy.FromWattHours(startWh);
}
