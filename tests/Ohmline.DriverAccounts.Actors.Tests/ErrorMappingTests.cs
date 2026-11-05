using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.DriverAccounts.Domain.Tokens;
using static Ohmline.DriverAccounts.Actors.Tests.TheCard;

namespace Ohmline.DriverAccounts.Actors.Tests;

public sealed class ErrorMappingTests
{
    private static readonly Dictionary<string, IdTokenError> Reasons = new()
    {
        [nameof(IdTokenError.NotRegistered)] = new IdTokenError.NotRegistered(Card),
        [nameof(IdTokenError.RegisteredToAnotherDriver)] =
            new IdTokenError.RegisteredToAnotherDriver(Card),
        [nameof(IdTokenError.TokenIsBlocked)] = new IdTokenError.TokenIsBlocked(Card),
        [nameof(IdTokenError.LockedByAnotherSession)] =
            new IdTokenError.LockedByAnotherSession(Card, AtTheDepot),
    };

    public static readonly TheoryData<string, string> Codes = new()
    {
        { nameof(IdTokenError.NotRegistered), "not-registered" },
        { nameof(IdTokenError.RegisteredToAnotherDriver), "registered-to-another-driver" },
        { nameof(IdTokenError.TokenIsBlocked), "token-blocked" },
        { nameof(IdTokenError.LockedByAnotherSession), "locked-by-another-session" },
    };

    [Theory]
    [MemberData(nameof(Codes))]
    public void Given_a_reason_when_mapped_then_the_reply_carries_its_code_and_message(
        string reason, string code)
    {
        var error = Reasons[reason];

        error.ToReply().ShouldBe(new TokenCommandRejected(code, error.Message));
    }

    [Fact]
    public void Every_reason_a_token_can_give_has_a_row() =>
        Reasons.Keys.ShouldBe(
            typeof(IdTokenError).GetConstructors()
                .Select(constructor => constructor.GetParameters().Single().ParameterType.Name),
            ignoreOrder: true);
}
