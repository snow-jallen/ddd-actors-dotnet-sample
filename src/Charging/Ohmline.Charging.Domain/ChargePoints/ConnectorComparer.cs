namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>Entity equality for connectors: one id is one connector, whatever its state.</summary>
public static class ConnectorComparer
{
    // The key selector is also called for null, so a missing connector has a null key.
    public static EqualityComparer<Connector> ById { get; } =
        EqualityComparer<Connector>.Create(connector => connector?.Id);
}
