namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>
/// What a connector is doing now. A placeholder until section 7.3 makes it a union whose cases
/// carry only the data valid in them.
/// </summary>
public sealed record ConnectorState(string Name)
{
    public static ConnectorState Available { get; } = new("Available");
}
