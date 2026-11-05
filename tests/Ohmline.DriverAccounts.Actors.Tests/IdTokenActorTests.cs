using Microsoft.Extensions.Time.Testing;
using Ohmline.DriverAccounts.Actors.Tokens;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;
using Ohmline.Testing;
using static Ohmline.DriverAccounts.Actors.Tests.TheCard;

namespace Ohmline.DriverAccounts.Actors.Tests;

/// <summary>
/// The id token's shell: a lock is answered with what the charging journey needs, the token's
/// driver or the reason it may not charge; every other command, with a version.
/// </summary>
public sealed class IdTokenActorTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(Serializers.DriverAccounts), nameof(IdTokenActorTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private PersistenceShellSpec.Scenario Token =>
        Shell(IdTokenActor.Props(Card, _clock), IdTokenActor.PersistenceIdOf(Card));

    [Fact]
    public void The_persistence_id_is_the_durable_type_name_and_the_token() =>
        IdTokenActor.PersistenceIdOf(Card).ShouldBe("id-token-04A2B3C4D5E6F7");

    [Fact]
    public async Task Given_an_unknown_token_when_registered_then_it_is_accepted_at_version_1()
    {
        var register = new RegisterToken(NewCommand, Card, Driver);

        await Token.Given()
            .When(register)
            .ThenAsync(
                new TokenCommandAccepted(1),
                Stamped(new TokenRegistered(Card, Driver), register.CommandId));
    }

    [Fact]
    public async Task Given_an_active_token_when_locked_then_the_journey_learns_whose_it_is()
    {
        var lockFor = new LockForSession(NewCommand, Card, AtTheDepot);

        await Token.Given(Stamped(new TokenRegistered(Card, Driver)))
            .When(lockFor)
            .ThenAsync(
                new TokenLocked(Card, AtTheDepot, Driver, Version: 2),
                Stamped(new TokenLockedForSession(Card, AtTheDepot), lockFor.CommandId));
    }

    [Fact]
    public async Task Given_a_token_held_by_a_session_when_another_asks_then_it_is_refused() =>
        await Token.Given(
                Stamped(new TokenRegistered(Card, Driver)),
                Stamped(new TokenLockedForSession(Card, AtTheDepot)))
            .When(new LockForSession(NewCommand, Card, ElsewhereAMinuteLater))
            .ThenAsync<TokenLockRefused>(refused =>
            {
                refused.SessionId.ShouldBe(ElsewhereAMinuteLater);
                refused.Code.ShouldBe("locked-by-another-session");
            });

    [Fact]
    public async Task Given_a_blocked_token_when_blocked_again_then_the_version_is_unchanged() =>
        await Token.Given(
                Stamped(new TokenRegistered(Card, Driver)), Stamped(new TokenBlocked(Card)))
            .When(new BlockToken(NewCommand, Card))
            .ThenAsync(new TokenCommandAccepted(2));

    [Fact]
    public async Task Given_a_token_held_by_a_session_when_asked_then_it_shows_that_session() =>
        await Token.Given(
                Stamped(new TokenRegistered(Card, Driver)),
                Stamped(new TokenLockedForSession(Card, AtTheDepot)))
            .When(new GetIdToken(Card))
            .ThenAsync(new IdTokenView(Card, 2, IdTokenView.Active, Driver, AtTheDepot));

    [Fact]
    public async Task Given_an_unknown_token_when_unlocked_then_it_is_already_satisfied() =>
        await Token.Given()
            .When(new UnlockFromSession(NewCommand, Card, AtTheDepot))
            .ThenAsync(new TokenCommandAccepted(0));
}
