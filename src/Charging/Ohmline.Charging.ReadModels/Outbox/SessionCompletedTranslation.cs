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
    public static SessionCompleted Translate(RelayedSession session, SessionEnded ended)
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

        return new SessionCompleted(
            EventIdOf(ended.SessionId),
            ended.OccurredAt,
            session.SessionId,
            session.TokenUid,
            session.ConnectorId,
            kWh,
            new PriceBreakdown(total.Currency.Code, total.Amount, lines),
            session.StartedAt,
            ended.OccurredAt);
    }

    public static Guid EventIdOf(SessionId sessionId) =>
        GuidV7.Derive(sessionId.Value, SessionCompleted.WireName);
}
