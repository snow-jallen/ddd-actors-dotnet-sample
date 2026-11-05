namespace Ohmline.Charging.Api.Responses;

/// <summary>
/// The body of a 202 Accepted: the aggregate's version after the command, which a client can
/// compare with the version of what it reads next.
/// </summary>
public sealed record CommandAcceptedResponse(long Version);
