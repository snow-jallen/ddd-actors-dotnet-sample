namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// A session as the session history has it. <paramref name="Version"/> is the session's
/// version when the row was written, and is also the response's <c>ETag</c>.
/// <paramref name="SiteId"/> came with the history's second version (section 20.9): an added,
/// optional field, which clients that do not know it skip; null while version 1 answers.
/// </summary>
public sealed record SessionResponse(
    Guid SessionId,
    string TokenUid,
    string ConnectorId,
    string Status,
    decimal EnergyKwh,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long Version,
    string? SiteId = null);
