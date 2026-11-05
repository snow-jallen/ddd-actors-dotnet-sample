using Ohmline.Charging.Contracts.Live;

namespace Ohmline.Charging.Api.App.Dtos;

/// <summary>
/// One change in a token's feed: a card as it became, at <paramref name="Position"/>. The hub
/// pushes the same shape as <c>driverCardChanged</c>, so an app applies both alike.
/// </summary>
public sealed record DriverAppFeedEntryDto(long Position, DriverAppCardDto Card)
{
    public static DriverAppFeedEntryDto From(DriverCardChanged changed) =>
        new(changed.Position, DriverAppCardDto.From(changed));
}
