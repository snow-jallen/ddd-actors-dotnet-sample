using Ohmline.ActorShell.Sharding;
using Ohmline.DriverAccounts.Contracts.Tokens;

namespace Ohmline.DriverAccounts.Actors;

/// <summary>Which id token each of Driver Accounts' messages is for: its UID.</summary>
public static class DriverAccountsMessageExtractors
{
    public static EntityMessageExtractor Tokens { get; } = new(message => message switch
    {
        IdTokenCommand command => command.TokenUid.Value,
        IdTokenQuery query => query.TokenUid.Value,
        _ => null,
    });
}
