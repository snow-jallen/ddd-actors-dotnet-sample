using Ohmline.ActorShell.Deciders;
using Ohmline.DriverAccounts.Actors.Tokens;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;
using static Ohmline.DriverAccounts.Actors.Tests.TheCard;

namespace Ohmline.DriverAccounts.Actors.Tests.Serialization;

/// <summary>One message of every type its serializer writes, all fields filled.</summary>
internal static class DriverAccountsSamples
{
    public static IReadOnlyList<object> All { get; } =
    [
        new RegisterToken(Command, Card, Driver),
        new BlockToken(Command, Card),
        new LockForSession(Command, Card, AtTheDepot),
        new UnlockFromSession(Command, Card, AtTheDepot),

        Stamped(new TokenRegistered(Card, Driver), Command),
        Stamped(new TokenBlocked(Card), Command),
        Stamped(new TokenLockedForSession(Card, AtTheDepot), Command),
        Stamped(new TokenUnlocked(Card, AtTheDepot), Command),

        new GetIdToken(Card),
        new IdTokenView(Card, 2, IdTokenView.Active, Driver, AtTheDepot),

        new TokenCommandAccepted(Version: 2),
        new TokenCommandRejected("not-registered", $"Token {Card} is not registered."),
        new TokenLocked(Card, AtTheDepot, Driver, Version: 2),
        new TokenLockRefused(
            Card,
            ElsewhereAMinuteLater,
            "locked-by-another-session",
            $"Token {Card} is charging in session {AtTheDepot}."),

        new IdTokenSnapshot(Card, Driver, Blocked: false, AtTheDepot)
        {
            RecentCommands = [new RecentCommand(Command, 2)],
        },
    ];
}
