using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Domain.Tokens;

/// <summary>
/// What an id token remembers: whose it is, whether it may charge, and the session it is
/// charging now, if any.
/// </summary>
public union IdTokenState(Unknown, Active, Blocked)
{
    public static IdTokenState Initial { get; } = new Unknown();

    /// <summary>Whose token it is, once it is registered.</summary>
    public DriverRef? Driver => this switch
    {
        Active active => active.Driver,
        Blocked blocked => blocked.Driver,
        _ => null,
    };

    /// <summary>The session that holds the token, if one does.</summary>
    public SessionId? ActiveSession => this switch
    {
        Active active => active.ActiveSession,
        Blocked blocked => blocked.ActiveSession,
        _ => null,
    };
}

/// <summary>No such token has been registered.</summary>
public sealed record Unknown;

/// <summary>The token may start a session, when it is not running one already.</summary>
public sealed record Active(TokenUid TokenUid, DriverRef Driver, SessionId? ActiveSession);

/// <summary>
/// The token starts no session. A session it started before it was blocked keeps it until the
/// session ends.
/// </summary>
public sealed record Blocked(TokenUid TokenUid, DriverRef Driver, SessionId? ActiveSession);
