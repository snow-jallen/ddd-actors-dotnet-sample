namespace Ohmline.Charging.Api.App.Dtos;

/// <summary>
/// A page of a token's feed, oldest first. <paramref name="Position"/> is where the next page
/// starts: the last entry's position, or the one asked for when there is nothing newer.
/// </summary>
public sealed record DriverAppFeedDto(IReadOnlyList<DriverAppFeedEntryDto> Entries, long Position);
