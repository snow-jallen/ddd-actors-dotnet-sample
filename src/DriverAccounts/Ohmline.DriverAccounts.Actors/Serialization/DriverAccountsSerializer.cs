using System.Collections.Frozen;
using Akka.Actor;
using Ohmline.ActorShell.Serialization;
using Ohmline.DriverAccounts.Actors.Tokens;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;

namespace Ohmline.DriverAccounts.Actors.Serialization;

/// <summary>
/// Everything Driver Accounts tells, asks, replies or persists, as JSON under a permanent
/// manifest. The identifier and the manifests are never changed or reused (ADR 0007).
/// </summary>
public sealed class DriverAccountsSerializer(ExtendedActorSystem system)
    : ManifestJsonSerializer(system, Manifests)
{
    /// <summary>The name Akka.NET knows the serializer by, in configuration and logs.</summary>
    public const string Name = "driver-accounts";

    /// <summary>Every message whose type derives from one of these is written here.</summary>
    public static IReadOnlyList<Type> BoundTypes { get; } =
    [
        typeof(IdTokenCommand),
        typeof(IdTokenEvent),
        typeof(IdTokenQuery),
        typeof(IdTokenView),
        typeof(TokenCommandReply),
        typeof(IdTokenSnapshot),
    ];

    public static FrozenDictionary<Type, string> Manifests { get; } = new Dictionary<Type, string>
    {
        [typeof(RegisterToken)] = "accounts.register-token.v1",
        [typeof(BlockToken)] = "accounts.block-token.v1",
        [typeof(LockForSession)] = "accounts.lock-for-session.v1",
        [typeof(UnlockFromSession)] = "accounts.unlock-from-session.v1",

        [typeof(TokenRegistered)] = "accounts.token-registered.v1",
        [typeof(TokenBlocked)] = "accounts.token-blocked.v1",
        [typeof(TokenLockedForSession)] = "accounts.token-locked-for-session.v1",
        [typeof(TokenUnlocked)] = "accounts.token-unlocked.v1",

        [typeof(GetIdToken)] = "accounts.get-id-token.v1",
        [typeof(IdTokenView)] = "accounts.id-token-view.v1",

        [typeof(TokenCommandAccepted)] = "accounts.token-command-accepted.v1",
        [typeof(TokenCommandRejected)] = "accounts.token-command-rejected.v1",
        [typeof(TokenLocked)] = "accounts.token-locked.v1",
        [typeof(TokenLockRefused)] = "accounts.token-lock-refused.v1",

        [typeof(IdTokenSnapshot)] = "accounts.id-token-snapshot.v1",
    }.ToFrozenDictionary();

    /// <summary>Permanent: Ohmline's serializer ids are listed in ADR 0007.</summary>
    public override int Identifier => 9103;
}
