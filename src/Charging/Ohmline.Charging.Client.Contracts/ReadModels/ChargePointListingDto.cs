namespace Ohmline.Charging.Client.Contracts.ReadModels;

/// <summary>
/// A charge point's listing as drivers see it, at its revision, from
/// <c>GET /app/v1/charge-points/{id}/listing</c>.
/// </summary>
public sealed record ChargePointListingDto(
    string ChargePointId,
    long Revision,
    string DisplayName,
    string AccessInstructions,
    string OpeningHours);
