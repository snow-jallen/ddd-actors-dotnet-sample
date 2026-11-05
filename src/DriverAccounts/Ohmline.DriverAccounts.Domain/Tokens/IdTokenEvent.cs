using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Domain.Tokens;

/// <summary>
/// What can happen to an id token. Written to the journal, so plain data only. Every event
/// records the command that caused it and when it happened.
/// </summary>
public closed record IdTokenEvent(TokenUid TokenUid)
{
    /// <summary>The command that caused this event: its causation.</summary>
    public CommandId CommandId { get; init; }

    /// <summary>When it happened: the time of the decision, from its context.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>The token was issued to this driver's account, for good.</summary>
public sealed record TokenRegistered(TokenUid TokenUid, DriverRef Driver)
    : IdTokenEvent(TokenUid);

/// <summary>The token starts no more sessions; one it already started may finish.</summary>
public sealed record TokenBlocked(TokenUid TokenUid) : IdTokenEvent(TokenUid);

/// <summary>The token is held by this session; no other may start with it.</summary>
public sealed record TokenLockedForSession(TokenUid TokenUid, SessionId SessionId)
    : IdTokenEvent(TokenUid);

/// <summary>The session that held the token has ended.</summary>
public sealed record TokenUnlocked(TokenUid TokenUid, SessionId SessionId)
    : IdTokenEvent(TokenUid);
