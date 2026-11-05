using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.ReadModels.Outbox;

/// <summary>
/// What the relay keeps of a session between its start and its end: the end names neither the
/// start time nor the tariff, and the integration event needs both. A row of
/// <c>read_models.integration_relay_sessions</c>, deleted once the session is published.
/// </summary>
public sealed record RelayedSession(
    Guid SessionId,
    string TokenUid,
    string ConnectorId,
    DateTimeOffset StartedAt,
    Money EnergyRate,
    Money SessionFees)
{
    /// <summary>
    /// The prices the session was started under, by the rule the journey captures by
    /// (<see cref="SessionTerms.CostOf"/>): what Billing is told and what was taken agree.
    /// </summary>
    public static RelayedSession From(SessionStarted started)
    {
        var terms = new SessionTerms(
            started.SessionId,
            started.SiteId,
            started.ConnectorId,
            started.TokenUid,
            started.Tariff,
            started.SpendCap);
        return new RelayedSession(
            started.SessionId.Value,
            started.TokenUid.Value,
            started.ConnectorId.ToString(),
            started.OccurredAt,
            terms.HighestEnergyRate,
            terms.SessionFees);
    }
}
