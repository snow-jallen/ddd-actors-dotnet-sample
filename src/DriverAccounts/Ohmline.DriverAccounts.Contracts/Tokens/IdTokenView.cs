using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Contracts.Tokens;

/// <summary>
/// The answer to <see cref="GetIdToken"/>, at <paramref name="Version"/>. <paramref name="Status"/>
/// is one of the constants below; a token never registered has version 0 and no driver.
/// </summary>
public sealed record IdTokenView(
    TokenUid TokenUid,
    long Version,
    string Status,
    DriverRef? Driver = null,
    SessionId? ActiveSession = null)
{
    public const string Unknown = "unknown";
    public const string Active = "active";
    public const string Blocked = "blocked";
}
