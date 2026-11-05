using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// What was agreed when the session started and never changes afterwards: where it runs, for
/// which token, under which tariff snapshot, and up to which amount.
/// </summary>
public sealed record SessionTerms(
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    TariffSnapshot Tariff,
    Money SpendCap)
{
    /// <summary>
    /// The most the energy delivered so far can cost: every kWh at the tariff's highest energy
    /// rate, plus the session fee. The spend cap is checked against this, so a session stops
    /// before its hold is used up; the price itself comes from the price calculator (8.3).
    /// </summary>
    public Money CostOf(Energy energy) =>
        SessionFees + HighestEnergyRate * energy.KilowattHours;

    /// <summary>A kWh in <see cref="CostOf"/>: the tariff's highest energy rate.</summary>
    public Money HighestEnergyRate =>
        Tariff.Components.OfType<EnergyRate>()
            .Select(rate => rate.PricePerKwh)
            .DefaultIfEmpty(Money.Zero(Tariff.Currency))
            .MaxBy(price => price.Amount);

    /// <summary>What starting the session costs: every session fee of the tariff.</summary>
    public Money SessionFees =>
        Tariff.Components.OfType<SessionFee>()
            .Aggregate(Money.Zero(Tariff.Currency), (fees, fee) => fees + fee.Amount);
}
