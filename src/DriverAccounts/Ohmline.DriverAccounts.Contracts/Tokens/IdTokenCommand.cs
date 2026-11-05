using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Contracts.Tokens;

/// <summary>
/// Everything an id token can be told. Every command names its token, which is how the shell
/// routes it, and carries the id its sender gave it.
/// </summary>
public closed record IdTokenCommand(CommandId CommandId, TokenUid TokenUid);

/// <summary>A new card or app credential, issued to one driver's account.</summary>
public sealed record RegisterToken(CommandId CommandId, TokenUid TokenUid, DriverRef Driver)
    : IdTokenCommand(CommandId, TokenUid);

/// <summary>The card is reported lost or misused: it may start no more sessions.</summary>
public sealed record BlockToken(CommandId CommandId, TokenUid TokenUid)
    : IdTokenCommand(CommandId, TokenUid);

/// <summary>
/// The charging journey asks to start this session with the token: yes, if the token is
/// active and not running another session. Answered with <see cref="TokenLocked"/> or
/// <see cref="TokenLockRefused"/>.
/// </summary>
public sealed record LockForSession(CommandId CommandId, TokenUid TokenUid, SessionId SessionId)
    : IdTokenCommand(CommandId, TokenUid);

/// <summary>The session has ended; the token may start another.</summary>
public sealed record UnlockFromSession(
    CommandId CommandId, TokenUid TokenUid, SessionId SessionId)
    : IdTokenCommand(CommandId, TokenUid);
