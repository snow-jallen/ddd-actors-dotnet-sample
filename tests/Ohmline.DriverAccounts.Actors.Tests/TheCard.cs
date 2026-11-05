using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Actors.Tests;

/// <summary>The primer's driver's card, its account, and two sessions a minute apart.</summary>
internal static class TheCard
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    public static readonly TokenUid Card = new("04A2B3C4D5E6F7");

    public static readonly DriverRef Driver = new("drv-7f3a91c2");

    public static readonly SessionId AtTheDepot =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001"));

    public static readonly SessionId ElsewhereAMinuteLater =
        new(Guid.Parse("019a4f6f-0680-7a51-8e2f-3b1d00000002"));

    /// <summary>A fixed id, for the samples and their golden files.</summary>
    public static readonly CommandId Command =
        new(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042"));

    /// <summary>A new id each time: every command its own (section 12.6).</summary>
    public static CommandId NewCommand => new(Guid.CreateVersion7(Morning));

    public static IdTokenEvent Stamped(IdTokenEvent @event, CommandId? by = null) =>
        @event with { CommandId = by ?? NewCommand, OccurredAt = Morning };
}
