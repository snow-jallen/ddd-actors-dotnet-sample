namespace Ohmline.Charging.Client.Contracts.Replies;

/// <summary>
/// The body of a 202 Accepted: the command, by the id the app gave it, and the aggregate's
/// version after it. The app keeps a pending overlay until what it reads has this version.
/// </summary>
public sealed record CommandAcceptedDto(Guid CommandId, long Version);
