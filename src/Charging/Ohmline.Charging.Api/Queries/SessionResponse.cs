namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// A session as the session history has it. <paramref name="Version"/> is the session's
/// version when the row was written, and is also the response's <c>ETag</c>.
/// </summary>
public sealed record SessionResponse(
    Guid SessionId,
    string TokenUid,
    string ConnectorId,
    string Status,
    decimal EnergyKwh,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long Version);
