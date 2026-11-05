using Ohmline.Charging.Client.Contracts.ReadModels;

namespace Ohmline.Charging.Client.Contracts.Notifications;

/// <summary>
/// A card changed: pushed to the token's group as <c>driverCardChanged</c>, with the
/// <paramref name="Position"/> of the same change in the token's feed, so an app applies a push
/// and a feed entry alike.
/// </summary>
public sealed record DriverCardChanged(long Position, DriverSessionCard Card);
