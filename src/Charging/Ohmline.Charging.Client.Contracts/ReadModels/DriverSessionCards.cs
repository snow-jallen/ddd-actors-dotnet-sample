namespace Ohmline.Charging.Client.Contracts.ReadModels;

/// <summary>
/// The app's home screen in one response: the session charging now, if any, the most recent
/// ones, and the feed <paramref name="Position"/> they are as of. The app reads the feed after
/// that position from then on.
/// </summary>
public sealed record DriverSessionCards(
    DriverSessionCard? Active, IReadOnlyList<DriverSessionCard> Recent, long Position);
