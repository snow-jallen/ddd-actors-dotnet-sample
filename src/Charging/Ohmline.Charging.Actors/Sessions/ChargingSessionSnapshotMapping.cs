using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Actors.Sessions;

/// <summary>From a session's state to its snapshot and back; exhaustive both ways.</summary>
public static class ChargingSessionSnapshotMapping
{
    extension(SessionState state)
    {
        /// <summary>Null before the session starts: there is nothing to keep.</summary>
        public ChargingSessionSnapshot? ToSnapshot() => state switch
        {
            NoSession => null,
            Active active => Of(
                active.Terms,
                new SessionPhaseSnapshot.Active(active.Meter.Start, active.Meter.Latest)),
            SuspendedByEv idle => Of(
                idle.Terms,
                new SessionPhaseSnapshot.SuspendedByEv(
                    idle.Meter.Start, idle.Meter.Latest, idle.Since)),
            SuspendedByCharger waiting => Of(
                waiting.Terms,
                new SessionPhaseSnapshot.SuspendedByCharger(
                    waiting.Meter.Start, waiting.Meter.Latest, waiting.Since)),
            Ended ended => Of(
                ended.Terms,
                new SessionPhaseSnapshot.Ended(ended.EnergyDelivered, ended.EndedAt)),
            null => throw new InvalidOperationException("The state is default: it holds no case."),
        };
    }

    extension(ChargingSessionSnapshot snapshot)
    {
        public SessionState ToState()
        {
            var terms = new SessionTerms(
                snapshot.SessionId,
                snapshot.SiteId,
                snapshot.ConnectorId,
                snapshot.TokenUid,
                snapshot.Tariff,
                snapshot.SpendCap)
            {
                PhaseCount = snapshot.PhaseCount ?? SessionTerms.AssumedPhaseCount,
                PricingModel = snapshot.PricingModel ?? PricingModel.Standard,
            };
            return snapshot.Phase switch
            {
                SessionPhaseSnapshot.Active active =>
                    new Active(terms, new SessionMeter(active.Start, active.Latest)),
                SessionPhaseSnapshot.SuspendedByEv idle => new SuspendedByEv(
                    terms, new SessionMeter(idle.Start, idle.Latest), idle.Since),
                SessionPhaseSnapshot.SuspendedByCharger waiting => new SuspendedByCharger(
                    terms, new SessionMeter(waiting.Start, waiting.Latest), waiting.Since),
                SessionPhaseSnapshot.Ended ended =>
                    new Ended(terms, ended.EnergyDelivered, ended.EndedAt),
            };
        }
    }

    private static ChargingSessionSnapshot Of(SessionTerms terms, SessionPhaseSnapshot phase) =>
        new(
            terms.SessionId,
            terms.SiteId,
            terms.ConnectorId,
            terms.TokenUid,
            terms.Tariff,
            terms.SpendCap,
            phase)
        {
            PhaseCount = terms.PhaseCount,
            PricingModel = terms.PricingModel,
        };
}
