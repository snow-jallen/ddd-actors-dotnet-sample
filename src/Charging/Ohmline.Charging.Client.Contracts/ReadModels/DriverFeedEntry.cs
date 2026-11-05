namespace Ohmline.Charging.Client.Contracts.ReadModels;

/// <summary>
/// One change in a token's feed: a card as it became, at <paramref name="Position"/>.
/// </summary>
public sealed record DriverFeedEntry(long Position, DriverSessionCard Card);
