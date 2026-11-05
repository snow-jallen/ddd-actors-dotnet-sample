using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// What was agreed when the session started and never changes afterwards: where it runs, for
/// which token, at which price, and up to which amount.
/// </summary>
public sealed record SessionTerms(
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    string TariffId,
    int TariffVersion,
    Money PricePerKwh,
    Money SpendCap)
{
    /// <summary>The energy price only, unrounded; section 8.3 prices a session properly.</summary>
    public Money CostOf(Energy energy) => PricePerKwh * energy.KilowattHours;
}
