using Ohmline.Charging.Client.Contracts.ReadModels;

namespace Ohmline.Charging.Client.Contracts.Notifications;

/// <summary>
/// A card changed: pushed to the token's group as <c>driverCardChanged</c>, with the
/// <paramref name="Position"/> of the same change in the token's feed, so an app applies a push
/// and a feed entry alike. <paramref name="TraceParent"/> (section 19.8) names the projection's
/// span, which a push cannot carry as a parent: the app links the span that applies it there.
/// Added after the first release, so it may be missing.
/// </summary>
public sealed record DriverCardChanged(
    long Position, DriverSessionCard Card, string? TraceParent = null);
