namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// What a read that asked for at least a version found (section 14.1): the read model has
/// caught up, or it is still behind. Only a default value holds neither.
/// </summary>
public union CatchUp<T>(CaughtUp<T>, StillBehind);

/// <summary>The read model has the version asked for, or a newer one.</summary>
public sealed record CaughtUp<T>(T Row);

/// <summary>
/// The read model is behind: <paramref name="Version"/> is the version it has, 0 when it has no
/// row at all yet.
/// </summary>
public sealed record StillBehind(long Version);
