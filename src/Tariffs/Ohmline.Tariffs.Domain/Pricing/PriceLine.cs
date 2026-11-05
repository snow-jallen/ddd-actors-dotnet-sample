using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Domain.Pricing;

/// <summary>
/// One line of a price breakdown, already rounded with the tariff's rounding policy. A closed
/// hierarchy, because a breakdown travels: to Billing and onto the driver's receipt.
/// </summary>
public closed record PriceLine(Money Amount);

/// <summary>The energy delivered at one energy rate: in its band, or outside every band.</summary>
public sealed record EnergyCharge(TimeOfUseBand? Band, Energy Energy, Money Rate, Money Amount)
    : PriceLine(Amount);

/// <summary>The time charged at one time rate.</summary>
public sealed record TimeCharge(TimeOfUseBand? Band, TimeSpan Duration, Money Rate, Money Amount)
    : PriceLine(Amount);

public sealed record SessionFeeCharge(Money Amount) : PriceLine(Amount);

/// <summary>Whole minutes plugged in after charging stopped, beyond the grace period.</summary>
public sealed record IdleFeeCharge(int Minutes, Money Rate, Money Amount) : PriceLine(Amount);

/// <summary>The tariff's price cap took this much off: a negative amount.</summary>
public sealed record PriceCapApplied(Money Cap, Money Amount) : PriceLine(Amount);
