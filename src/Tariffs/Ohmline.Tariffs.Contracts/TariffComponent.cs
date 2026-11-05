using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Tariffs.Contracts;

/// <summary>
/// One kind of pricing rule. A tariff is a list of these, which is what makes tariffs data that
/// a pricing analyst publishes rather than code that a developer changes: the Knowledge Level of
/// Ohmline's pricing (section 8.2). A closed hierarchy rather than a union, because a tariff is
/// persisted inside <c>SessionStarted</c> and by the tariff book (ADR 0003). A new kind of rule
/// is a new case here, and every switch over the components stops compiling until it is priced.
/// </summary>
public closed record TariffComponent;

/// <summary>
/// A price per kWh delivered. With a band it applies only inside the band; without one it
/// applies whenever no band of an energy rate does.
/// </summary>
public sealed record EnergyRate(Money PricePerKwh, TimeOfUseBand? Band = null) : TariffComponent;

/// <summary>A price per minute of charging, with or without a band, like an energy rate.</summary>
public sealed record TimeRate(Money PricePerMinute, TimeOfUseBand? Band = null) : TariffComponent;

/// <summary>A fixed amount for starting a session.</summary>
public sealed record SessionFee(Money Amount) : TariffComponent;

/// <summary>
/// A price per minute that the car stays plugged in after it has stopped charging, once the
/// grace period is over.
/// </summary>
public sealed record IdleFee(Money PricePerMinute, TimeSpan GracePeriod) : TariffComponent;
