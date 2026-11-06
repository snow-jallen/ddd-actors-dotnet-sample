namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// How a session's fees apply, chosen when it starts and recorded in <c>SessionStarted</c>
/// (section 21.8). Durable values: they are in the journal for ever.
/// </summary>
public static class PricingModel
{
    /// <summary>The tariff as it is written. Every session started before section 21.8.</summary>
    public const string Standard = "standard";

    /// <summary>
    /// The dynamic idle fee pilot: the idle fee starts after half the tariff's grace period, so
    /// a car that has finished charging frees the connector sooner.
    /// </summary>
    public const string DynamicIdleFee = "dynamic-idle-fee";
}

/// <summary>
/// The feature flags Charging's deciders read, by the names the feature manager knows them by.
/// The shell evaluates them; Decide reads them from its context; Evolve never does.
/// </summary>
public static class ChargingFeatures
{
    /// <summary>Sessions start under <see cref="PricingModel.DynamicIdleFee"/>.</summary>
    public const string DynamicIdleFee = "DynamicIdleFee";

    public static IReadOnlyList<string> All { get; } = [DynamicIdleFee];
}
