using Ohmline.DriverAccounts.Contracts.Tokens;

namespace Ohmline.DriverAccounts.Api.Responses;

/// <summary>A registered token: whose it is, its status, and the session holding it.</summary>
public sealed record TokenResponse(
    string TokenUid, long Version, string Status, string Driver, Guid? ActiveSession)
{
    public static TokenResponse From(IdTokenView view, DriverRef driver) => new(
        view.TokenUid.Value, view.Version, view.Status, driver.Value, view.ActiveSession?.Value);
}

/// <summary>The body of a 202 Accepted: the token's version after the command.</summary>
public sealed record CommandAcceptedResponse(long Version);
