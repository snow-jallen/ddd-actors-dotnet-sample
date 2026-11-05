namespace Ohmline.Charging.Api.Requests;

/// <summary>The whole new listing of a charge point, as operators write it.</summary>
public sealed record ListingRequest(
    string DisplayName, string AccessInstructions, string OpeningHours);
