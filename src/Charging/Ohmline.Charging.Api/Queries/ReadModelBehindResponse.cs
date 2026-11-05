namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// The body of a 202 from a read with <c>?minVersion=</c>: the read model has
/// <paramref name="Version"/> so far, and the client asked for <paramref name="MinVersion"/>.
/// </summary>
public sealed record ReadModelBehindResponse(long Version, long MinVersion);
