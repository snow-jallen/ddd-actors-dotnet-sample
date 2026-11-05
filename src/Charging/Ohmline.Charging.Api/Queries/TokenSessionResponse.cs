namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// One session in a driver token's history. <paramref name="Version"/> is the session's
/// version when the row was written: what read-your-writes compares with (section 14.1).
/// </summary>
public sealed record TokenSessionResponse(
    Guid SessionId,
    string ConnectorId,
    string Status,
    decimal EnergyKwh,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long Version);
