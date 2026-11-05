namespace Ohmline.Charging.Contracts;

/// <summary>
/// What an aggregate in Charging answers to a command: accepted or rejected, and nothing in
/// between. A closed record, not a union, because a reply crosses the wire: it is told back to
/// the sender, through the cluster, under a manifest of its own (ADR 0003).
/// </summary>
public closed record CommandReply;

/// <summary>
/// The command was decided and every event it caused is in the journal. The version is the
/// aggregate's sequence number afterwards: the number of events it has ever recorded. A command
/// that changed nothing answers with the version it found.
/// </summary>
public sealed record CommandAccepted(long Version) : CommandReply;

/// <summary>
/// The command was refused and nothing was recorded. The code is stable and meant for programs
/// (<c>reserved-for-another-token</c>); the message is a sentence for people and logs, and may
/// change wording at any time.
/// </summary>
public sealed record CommandRejected(string Code, string Message) : CommandReply;
