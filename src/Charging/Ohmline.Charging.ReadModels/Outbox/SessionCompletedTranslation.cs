using System.Collections.Immutable;
using Ohmline.Charging.Contracts.Integration;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.ReadModels.Outbox;

/// <summary>
/// Charging's domain events in its Published Language (section 16.1): a pure function, so what
/// Billing reads is decided by a table of examples, not by a database.
/// </summary>
public static class SessionCompletedTranslation
{
    /// <summary>
    /// The session that started as <paramref name="session"/> and ended as
    /// <paramref name="ended"/>, priced. The event id is derived from the session's, so the same
    /// session is always the same event, however often it is translated.
    /// </summary>
    public static SessionCompleted Translate(RelayedSession session, SessionEnded ended) =>
        new(
            EventIdOf(ended.SessionId),
            ended.OccurredAt,
            session.SessionId,
            session.TokenUid,
            session.ConnectorId,
            ended.EnergyDelivered.KilowattHours,
            Price(session, ended),
            session.StartedAt,
            ended.OccurredAt);

    /// <summary>
    /// The same session in the second version (section 20.9), from the same facts: the site and
    /// the connector come from the event that ended it.
    /// </summary>
    public static SessionCompletedV2 TranslateV2(RelayedSession session, SessionEnded ended) =>
        new(
            EventIdOf(ended.SessionId, SessionCompletedV2.WireName),
            ended.OccurredAt,
            session.SessionId,
            ended.SiteId.Value,
            session.TokenUid,
            ended.ConnectorId.ChargePointId.Value,
            ended.ConnectorId.Number,
            ended.EnergyDelivered.KilowattHours,
            Price(session, ended),
            session.StartedAt,
            ended.OccurredAt);

    public static Guid EventIdOf(SessionId sessionId) =>
        EventIdOf(sessionId, SessionCompleted.WireName);

    /// <summary>One id per session and version: the same every time, never another's.</summary>
    public static Guid EventIdOf(SessionId sessionId, string wireName) =>
        GuidV7.Derive(sessionId.Value, wireName);

    private static PriceBreakdown Price(RelayedSession session, SessionEnded ended)
    {
        var kWh = ended.EnergyDelivered.KilowattHours;
        var energy = session.EnergyRate * kWh;
        ImmutableArray<PriceLine> lines =
        [
            new(PriceLine.Energy, kWh, "kWh", session.EnergyRate.Amount, energy.Amount),
            .. session.SessionFees.Amount is 0m
                ? ImmutableArray<PriceLine>.Empty
                : [new(PriceLine.SessionFee, 1m, "session", session.SessionFees.Amount,
                    session.SessionFees.Amount)],
        ];
        var total = energy + session.SessionFees;
        return new PriceBreakdown(total.Currency.Code, total.Amount, lines);
    }
}
