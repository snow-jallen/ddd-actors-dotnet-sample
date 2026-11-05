using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Contracts.Tokens;

/// <summary>
/// What an id token answers to a command. A closed record, not a union, because it is told
/// back across the cluster (ADR 0003). A lock gets an answer of its own, which carries what the
/// charging journey needs next; every other command is accepted or rejected.
/// </summary>
public closed record TokenCommandReply;

/// <summary>Every event the command caused is stored; the version is the token's now.</summary>
public sealed record TokenCommandAccepted(long Version) : TokenCommandReply;

/// <summary>Refused and nothing recorded: a stable code, and a sentence for people.</summary>
public sealed record TokenCommandRejected(string Code, string Message) : TokenCommandReply;

/// <summary>
/// The token is locked for this session, and no other may start with it until it is unlocked.
/// The driver's account is who pays.
/// </summary>
public sealed record TokenLocked(
    TokenUid TokenUid, SessionId SessionId, DriverRef Driver, long Version)
    : TokenCommandReply;

/// <summary>
/// The token may not start this session: it is unknown, blocked, or running another session.
/// The code says which.
/// </summary>
public sealed record TokenLockRefused(
    TokenUid TokenUid, SessionId SessionId, string Code, string Message)
    : TokenCommandReply;
