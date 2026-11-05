using Ohmline.DriverAccounts.Domain.Tokens;
using static Ohmline.DriverAccounts.Domain.Tests.TheCard;

namespace Ohmline.DriverAccounts.Domain.Tests;

/// <summary>
/// One active session per token, one account per token, and no session for a blocked token:
/// every arm of the id token's decision table, and Evolve.
/// </summary>
public sealed class IdTokenTests
{
    [Fact]
    public void Given_an_unknown_token_when_registered_then_it_belongs_to_the_driver() =>
        IdTokenSpec.Given().At(Morning)
            .When(Register())
            .Then(new TokenRegistered(Card, Driver).At(Morning));

    [Fact]
    public void Given_a_registered_token_when_registered_to_its_driver_again_then_no_change() =>
        IdTokenSpec.Given(Registered()).At(Morning)
            .When(Register())
            .ThenNothingChanges();

    [Fact]
    public void Given_a_registered_token_when_registered_to_another_driver_then_it_is_refused() =>
        IdTokenSpec.Given(Registered()).At(Morning)
            .When(Register(SomeoneElse))
            .ThenRejects(new IdTokenError.RegisteredToAnotherDriver(Card));

    [Fact]
    public void Given_an_active_token_when_locked_for_a_session_then_the_session_holds_it() =>
        IdTokenSpec.Given(Registered()).At(Morning)
            .When(Lock(AtTheDepot))
            .Then(new TokenLockedForSession(Card, AtTheDepot).At(Morning));

    [Fact]
    public void Given_a_token_held_by_a_session_when_it_locks_again_then_nothing_changes() =>
        IdTokenSpec.Given(Registered(), Locked(AtTheDepot)).At(Morning)
            .When(Lock(AtTheDepot))
            .ThenNothingChanges();

    [Fact]
    public void Given_a_token_held_by_a_session_when_another_session_asks_then_it_is_refused() =>
        IdTokenSpec.Given(Registered(), Locked(AtTheDepot)).At(Morning.AddMinutes(1))
            .When(Lock(ElsewhereAMinuteLater))
            .ThenRejects(new IdTokenError.LockedByAnotherSession(Card, AtTheDepot));

    [Fact]
    public void Given_an_unknown_token_when_locked_then_it_is_refused() =>
        IdTokenSpec.Given().At(Morning)
            .When(Lock(AtTheDepot))
            .ThenRejects(new IdTokenError.NotRegistered(Card));

    [Fact]
    public void Given_an_unknown_token_when_blocked_then_it_is_refused() =>
        IdTokenSpec.Given().At(Morning)
            .When(Block())
            .ThenRejects(new IdTokenError.NotRegistered(Card));

    [Fact]
    public void Given_an_active_token_when_blocked_then_it_is_blocked() =>
        IdTokenSpec.Given(Registered()).At(Morning)
            .When(Block())
            .Then(new TokenBlocked(Card).At(Morning));

    [Fact]
    public void Given_a_blocked_token_when_blocked_again_then_nothing_changes() =>
        IdTokenSpec.Given(Registered(), Blocked()).At(Morning)
            .When(Block())
            .ThenNothingChanges();

    [Fact]
    public void Given_a_blocked_token_when_locked_then_it_is_refused() =>
        IdTokenSpec.Given(Registered(), Blocked()).At(Morning)
            .When(Lock(AtTheDepot))
            .ThenRejects(new IdTokenError.TokenIsBlocked(Card));

    [Fact]
    public void Given_a_blocked_token_when_registered_to_its_driver_then_nothing_changes() =>
        IdTokenSpec.Given(Registered(), Blocked()).At(Morning)
            .When(Register())
            .ThenNothingChanges();

    [Fact]
    public void Given_a_token_held_by_a_session_when_that_session_unlocks_it_then_it_is_free() =>
        IdTokenSpec.Given(Registered(), Locked(AtTheDepot)).At(Morning)
            .When(Unlock(AtTheDepot))
            .Then(new TokenUnlocked(Card, AtTheDepot).At(Morning));

    [Fact]
    public void Given_a_session_that_started_before_the_block_when_it_ends_then_it_unlocks() =>
        IdTokenSpec.Given(Registered(), Locked(AtTheDepot), Blocked()).At(Morning)
            .When(Unlock(AtTheDepot))
            .Then(new TokenUnlocked(Card, AtTheDepot).At(Morning));

    [Theory]
    [MemberData(nameof(UnlocksThatAreAlreadySatisfied))]
    public void Given_an_unlock_that_is_already_satisfied_then_nothing_changes(
        string history) =>
        IdTokenSpec.Given(Histories[history]).At(Morning)
            .When(Unlock(AtTheDepot))
            .ThenNothingChanges();

    public static TheoryData<string> UnlocksThatAreAlreadySatisfied =>
        new(Histories.Keys);

    private static readonly Dictionary<string, IdTokenEvent[]> Histories = new()
    {
        ["unknown"] = [],
        ["active and free"] = [Registered()],
        ["held by another session"] = [Registered(), Locked(ElsewhereAMinuteLater)],
        ["blocked and free"] = [Registered(), Blocked()],
    };

    [Fact]
    public void Given_a_history_then_the_state_is_its_fold()
    {
        var state = new[]
        {
            Registered(),
            Locked(AtTheDepot),
            Blocked(),
            new TokenUnlocked(Card, AtTheDepot).At(Morning),
        }.Aggregate(IdToken.Initial, IdToken.Evolve);

        state.Value.ShouldBe(new Blocked(Card, Driver, ActiveSession: null));
    }

    [Fact]
    public void Given_an_event_that_does_not_apply_then_evolve_leaves_the_state_as_it_is() =>
        IdToken.Evolve(IdToken.Initial, Locked(AtTheDepot)).Value.ShouldBeOfType<Unknown>();
}
