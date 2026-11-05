namespace Ohmline.Charging.Api.Responses;

/// <summary>A charge point's listing as drivers see it, at its revision.</summary>
public sealed record ListingResponse(
    string ChargePointId,
    long Revision,
    string DisplayName,
    string AccessInstructions,
    string OpeningHours);
