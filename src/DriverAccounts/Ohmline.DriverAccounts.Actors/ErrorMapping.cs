using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;

namespace Ohmline.DriverAccounts.Actors;

/// <summary>
/// From the token's reasons to Driver Accounts' wire replies, with an exhaustive switch. Codes
/// are published vocabulary: add new ones, never change or reuse one.
/// </summary>
public static class ErrorMapping
{
    extension(IdTokenError error)
    {
        public string Code => error switch
        {
            IdTokenError.NotRegistered => "not-registered",
            IdTokenError.RegisteredToAnotherDriver => "registered-to-another-driver",
            IdTokenError.TokenIsBlocked => "token-blocked",
            IdTokenError.LockedByAnotherSession => "locked-by-another-session",
            null => throw new InvalidOperationException(
                "This IdTokenError is default: it holds no case to report."),
        };

        public TokenCommandRejected ToReply() => new(error.Code, error.Message);
    }
}
