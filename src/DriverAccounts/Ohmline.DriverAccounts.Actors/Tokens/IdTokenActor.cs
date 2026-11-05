using System.Collections.Immutable;
using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Actors.Tokens;

/// <summary>
/// One id token's decider in the shell. Every rule is in <see cref="IdToken"/>; what is
/// particular here is the answer to a lock, which tells the charging journey whose token it is,
/// or why it may not charge.
/// </summary>
public sealed class IdTokenActor
    : DeciderActor<IdToken, IdTokenState, IdTokenCommand, IdTokenEvent, IdTokenError>
{
    /// <summary>Durable vocabulary: the shard type name and the persistence id prefix.</summary>
    public const string TypeName = "id-token";

    public IdTokenActor(TokenUid tokenUid, TimeProvider clock)
        : base(TypeName, tokenUid.Value, clock) =>
        Command<GetIdToken>(_ => Sender.Tell(State.ToView(tokenUid, LastSequenceNr)));

    /// <summary>Durable vocabulary: <c>id-token-04A2B3C4D5E6F7</c>, for ever.</summary>
    public static string PersistenceIdOf(TokenUid tokenUid) => $"{TypeName}-{tokenUid}";

    /// <summary>
    /// For a test that starts the actor itself, with a clock of its choosing. In a node, the
    /// shard region has the container make it (section 12.3).
    /// </summary>
    public static Props Props(TokenUid tokenUid, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new IdTokenActor(tokenUid, clock));

    protected override object Accepted(long version) => new TokenCommandAccepted(version);

    protected override object Accepted(IdTokenCommand command, long version) => command switch
    {
        LockForSession lockFor when State.Driver is { } driver =>
            new TokenLocked(lockFor.TokenUid, lockFor.SessionId, driver, version),
        _ => Accepted(version),
    };

    protected override object Rejected(IdTokenError error) => error.ToReply();

    protected override object Rejected(IdTokenCommand command, IdTokenError error) =>
        command switch
        {
            LockForSession lockFor => new TokenLockRefused(
                lockFor.TokenUid, lockFor.SessionId, error.Code, error.Message),
            _ => Rejected(error),
        };

    protected override object Rejected(string code, string message) =>
        new TokenCommandRejected(code, message);

    protected override CommandId CommandIdOf(IdTokenCommand command) => command.CommandId;

    protected override CommandId CausationOf(IdTokenEvent @event) => @event.CommandId;

    protected override object? ToSnapshot(
        IdTokenState state, ImmutableArray<RecentCommand> recent) =>
        state.ToSnapshot() is { } snapshot ? snapshot with { RecentCommands = recent } : null;

    protected override (IdTokenState State, ImmutableArray<RecentCommand> Recent) FromSnapshot(
        object snapshot)
    {
        var stored = (IdTokenSnapshot)snapshot;
        return (stored.ToState(), stored.RecentCommands);
    }
}
