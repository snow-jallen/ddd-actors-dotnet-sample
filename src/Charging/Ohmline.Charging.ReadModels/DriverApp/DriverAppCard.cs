using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.ReadModels.DriverApp;

/// <summary>
/// One session as the driver app's session card shows it (section 14.6): a row of
/// <c>read_models.driver_app_cards</c>, and, as <see cref="DriverSessionCard"/> in the client
/// contracts' JSON (section 19.2), the payload of the token's change feed. It
/// is shaped for one screen: everything the card shows, with the charge point's display name
/// already in it, so a phone on a cellular link makes one request, not one per charge point.
/// <paramref name="Version"/> is the session's version, the sequence number of its last event
/// applied. Times are in UTC, as the app gets them, to the microsecond, as PostgreSQL keeps them:
/// a card reads the same from its table and from the feed.
/// </summary>
public sealed record DriverAppCard(
    string TokenUid,
    Guid SessionId,
    string Status,
    string ConnectorId,
    string? ChargePointDisplayName,
    decimal EnergyKwh,
    long MeterStartWh,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long Version)
{
    /// <summary>
    /// The card in the published language for client apps: what the app shows, without the
    /// meter's start, which only the projection needs.
    /// </summary>
    public DriverSessionCard ToClient() => new(
        SessionId,
        TokenUid,
        Status,
        ConnectorId,
        ChargePointDisplayName,
        EnergyKwh,
        StartedAt,
        EndedAt,
        Version);

    /// <summary>
    /// The card after one more of the session's events, numbered <paramref name="sequenceNr"/>:
    /// a pure function. <paramref name="displayName"/> is the charge point's display name if
    /// the projection knows it by now; a card keeps the name it was first given, the one the
    /// charge point had when the session started. Null while there is no card and the event is
    /// not the session's start.
    /// </summary>
    public static DriverAppCard? Apply(
        DriverAppCard? card, SessionEvent @event, long sequenceNr, string? displayName)
    {
        var next = (card, @event) switch
        {
            (null, SessionStarted started) => new DriverAppCard(
                started.TokenUid.Value,
                started.SessionId.Value,
                DriverAppCardStatus.Charging,
                started.ConnectorId.ToString(),
                ChargePointDisplayName: null,
                EnergyKwh: 0m,
                started.MeterStartWh,
                Stored(started.OccurredAt),
                EndedAt: null,
                sequenceNr),
            (null, _) => null,
            (_, MeterValueRecorded { Phase: null } recorded) => card with
            {
                EnergyKwh = DeliveredSince(card.MeterStartWh, recorded.Register).KilowattHours,
            },
            (_, ChargingSuspendedByEv) => card with { Status = DriverAppCardStatus.PausedByCar },
            (_, ChargingSuspendedByCharger) =>
                card with { Status = DriverAppCardStatus.PausedByCharger },
            (_, ChargingResumed) => card with { Status = DriverAppCardStatus.Charging },
            (_, SessionEnded ended) => card with
            {
                Status = DriverAppCardStatus.Ended,
                EnergyKwh = ended.EnergyDelivered.KilowattHours,
                EndedAt = Stored(ended.OccurredAt),
            },
            (_, SessionStarted or MeterValueRecorded or MeterAnomalyDetected or SpendCapReached) =>
                card,
        };

        return next is null
            ? null
            : next with
            {
                ChargePointDisplayName = next.ChargePointDisplayName ?? displayName,
                Version = sequenceNr,
            };
    }

    private static Energy DeliveredSince(long startWh, Energy register) =>
        register - Energy.FromWattHours(startWh);

    private static DateTimeOffset Stored(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
