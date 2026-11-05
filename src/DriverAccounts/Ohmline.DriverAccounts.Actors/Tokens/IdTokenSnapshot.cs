using System.Collections.Immutable;
using Ohmline.ActorShell.Deciders;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Actors.Tokens;

/// <summary>
/// A registered id token, as its snapshot stores it. A DTO with a manifest of its own, never the
/// state union. Equal when the values are equal.
/// </summary>
public sealed record IdTokenSnapshot(
    TokenUid TokenUid, DriverRef Driver, bool Blocked, SessionId? ActiveSession)
{
    /// <summary>The commands it accepted most recently (section 12.6).</summary>
    public ImmutableArray<RecentCommand> RecentCommands
    {
        get;
        init => field = value.IsDefault ? [] : value;
    } = [];

    public bool Equals(IdTokenSnapshot? other) =>
        other is not null
        && TokenUid == other.TokenUid
        && Driver == other.Driver
        && Blocked == other.Blocked
        && ActiveSession == other.ActiveSession
        && RecentCommands.SequenceEqual(other.RecentCommands);

    public override int GetHashCode() => HashCode.Combine(TokenUid, Driver, Blocked);
}
