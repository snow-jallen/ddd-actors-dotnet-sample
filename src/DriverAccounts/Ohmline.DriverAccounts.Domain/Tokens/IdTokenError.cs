using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Domain.Tokens;

/// <summary>
/// Why an id token refused a command. The cases live inside the union because one of them
/// shares a name with a state: <c>Blocked</c> is what the token is, and
/// <c>IdTokenError.TokenIsBlocked</c> is the reason it gives.
/// </summary>
public union IdTokenError(
    IdTokenError.NotRegistered,
    IdTokenError.RegisteredToAnotherDriver,
    IdTokenError.TokenIsBlocked,
    IdTokenError.LockedByAnotherSession)
{
    /// <summary>A sentence for logs and people. Code decides by the case, never by this.</summary>
    public string Message => this switch
    {
        NotRegistered error => $"Token {error.TokenUid} is not registered.",
        RegisteredToAnotherDriver error =>
            $"Token {error.TokenUid} already belongs to another driver.",
        TokenIsBlocked error => $"Token {error.TokenUid} is blocked.",
        LockedByAnotherSession error =>
            $"Token {error.TokenUid} is charging in session {error.ActiveSession}.",
        null => "This IdTokenError is default: it holds no case.",
    };

    public sealed record NotRegistered(TokenUid TokenUid);

    /// <summary>A token belongs to one account: registering it to another is refused.</summary>
    public sealed record RegisteredToAnotherDriver(TokenUid TokenUid);

    public sealed record TokenIsBlocked(TokenUid TokenUid);

    /// <summary>One active session per token: the same card tapped at two sites at once.</summary>
    public sealed record LockedByAnotherSession(TokenUid TokenUid, SessionId ActiveSession);
}
