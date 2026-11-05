namespace Ohmline.Charging.Client.Contracts.ReadModels;

/// <summary>
/// A page of a token's feed, oldest first, from <c>GET /app/v1/tokens/{uid}/feed?after=</c>.
/// <paramref name="Position"/> is where the next page starts: the last entry's position, or the
/// one asked for when there is nothing newer.
/// </summary>
public sealed record DriverFeedPage(IReadOnlyList<DriverFeedEntry> Entries, long Position);
