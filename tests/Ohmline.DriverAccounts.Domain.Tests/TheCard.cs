using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Domain.Tests;

/// <summary>
/// The primer's driver's RFID card, the account it belongs to, and the sessions it starts at
/// the bus depot and, a minute later, somewhere else (hotspot H7). Fixed values.
/// </summary>
internal static class TheCard
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly TokenUid Card = new("04A2B3C4D5E6F7");

    public static readonly DriverRef Driver = new("drv-7f3a91c2");

    public static readonly DriverRef SomeoneElse = new("drv-0b5e44d1");

    public static readonly SessionId AtTheDepot =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001"));

    public static readonly SessionId ElsewhereAMinuteLater =
        new(Guid.Parse("019a4f6f-0680-7a51-8e2f-3b1d00000002"));

    public static readonly CommandId Command =
        new(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042"));

    public static RegisterToken Register(DriverRef? driver = null) =>
        new(Command, Card, driver ?? Driver);

    public static LockForSession Lock(SessionId session) => new(Command, Card, session);

    public static UnlockFromSession Unlock(SessionId session) => new(Command, Card, session);

    public static BlockToken Block() => new(Command, Card);

    /// <summary>A history event, stamped as a decision would stamp it.</summary>
    public static IdTokenEvent At(this IdTokenEvent @event, DateTimeOffset time) =>
        @event with { CommandId = Command, OccurredAt = time };

    public static IdTokenEvent Registered() => new TokenRegistered(Card, Driver).At(Morning);

    public static IdTokenEvent Locked(SessionId session) =>
        new TokenLockedForSession(Card, session).At(Morning);

    public static IdTokenEvent Blocked() => new TokenBlocked(Card).At(Morning);
}
