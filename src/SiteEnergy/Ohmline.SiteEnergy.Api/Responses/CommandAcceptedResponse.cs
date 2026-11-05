namespace Ohmline.SiteEnergy.Api.Responses;

/// <summary>The body of a 202 Accepted: the budget's version after the command.</summary>
public sealed record CommandAcceptedResponse(long Version);
