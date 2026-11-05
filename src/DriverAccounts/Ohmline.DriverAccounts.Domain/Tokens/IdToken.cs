using System.Collections.Immutable;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;

namespace Ohmline.DriverAccounts.Domain.Tokens;

/// <summary>
/// An id token as a decider, keyed by its UID. "One active session per token" looks like a rule
/// about every session in Ohmline; it is a rule about one token. An aggregate per token makes
/// it a check of one field, decided one command at a time, in the token's own mailbox: two
/// journeys that ask at once are put in order, and the second is refused.
/// </summary>
public sealed class IdToken
    : IDecider<IdTokenState, IdTokenCommand, IdTokenEvent, IdTokenError>
{
    private IdToken()
    {
    }

    public static IdTokenState Initial => IdTokenState.Initial;

    /// <summary>
    /// Decides, then stamps every event of the decision with the command that caused it and
    /// the context's time.
    /// </summary>
    public static Result<ImmutableArray<IdTokenEvent>, IdTokenError> Decide(
        IdTokenState state, IdTokenCommand command, DecisionContext context) =>
        DecisionTable(state, command).Map(events => Stamp(events, command, context));

    /// <summary>Total: an event that does not apply to the state leaves it as it is.</summary>
    public static IdTokenState Evolve(IdTokenState state, IdTokenEvent @event) =>
        (state, @event) switch
        {
            (Unknown, TokenRegistered registered) =>
                new Active(registered.TokenUid, registered.Driver, ActiveSession: null),
            (Active active, TokenBlocked) =>
                new Blocked(active.TokenUid, active.Driver, active.ActiveSession),
            (Active active, TokenLockedForSession locked) =>
                active with { ActiveSession = locked.SessionId },
            (Active active, TokenUnlocked) => active with { ActiveSession = null },
            (Blocked blocked, TokenUnlocked) => blocked with { ActiveSession = null },
            (null, _) => throw DefaultState(),
            _ => state,
        };

    private static Result<ImmutableArray<IdTokenEvent>, IdTokenError> DecisionTable(
        IdTokenState state, IdTokenCommand command) =>
        (state, command) switch
        {
            (Unknown, RegisterToken register) =>
                Accept(new TokenRegistered(register.TokenUid, register.Driver)),
            (Unknown, BlockToken or LockForSession) =>
                Reject(new IdTokenError.NotRegistered(command.TokenUid)),
            (Unknown, UnlockFromSession) => NoChange,

            (Active or Blocked, RegisterToken register) when state.Driver == register.Driver =>
                NoChange,
            (Active or Blocked, RegisterToken) =>
                Reject(new IdTokenError.RegisteredToAnotherDriver(command.TokenUid)),

            (Active, BlockToken block) => Accept(new TokenBlocked(block.TokenUid)),
            (Blocked, BlockToken) => NoChange,

            (Active { ActiveSession: null }, LockForSession lockFor) =>
                Accept(new TokenLockedForSession(lockFor.TokenUid, lockFor.SessionId)),
            (Active active, LockForSession lockFor)
                when active.ActiveSession == lockFor.SessionId => NoChange,
            (Active { ActiveSession: { } other }, LockForSession) =>
                Reject(new IdTokenError.LockedByAnotherSession(command.TokenUid, other)),
            (Blocked, LockForSession) => Reject(new IdTokenError.TokenIsBlocked(command.TokenUid)),

            (Active or Blocked, UnlockFromSession unlock)
                when state.ActiveSession == unlock.SessionId =>
                Accept(new TokenUnlocked(unlock.TokenUid, unlock.SessionId)),
            (Active or Blocked, UnlockFromSession) => NoChange,

            (null, _) => throw DefaultState(),
        };

    private static Result<ImmutableArray<IdTokenEvent>, IdTokenError> Accept(
        IdTokenEvent @event) => Result.Ok<ImmutableArray<IdTokenEvent>>([@event]);

    private static Result<ImmutableArray<IdTokenEvent>, IdTokenError> NoChange { get; } =
        Result.Ok(ImmutableArray<IdTokenEvent>.Empty);

    private static Result<ImmutableArray<IdTokenEvent>, IdTokenError> Reject(
        IdTokenError error) => Result.Fail(error);

    private static ImmutableArray<IdTokenEvent> Stamp(
        ImmutableArray<IdTokenEvent> events, IdTokenCommand command, DecisionContext context) =>
        [.. events.Select(@event =>
            @event with { CommandId = command.CommandId, OccurredAt = context.Now })];

    private static InvalidOperationException DefaultState() =>
        new("The state is default: it holds no case. Start from IdTokenState.Initial.");
}
