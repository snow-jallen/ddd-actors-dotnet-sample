using Ohmline.DriverAccounts.Actors.Tests.Serialization;
using Ohmline.DriverAccounts.Contracts.Tokens;

namespace Ohmline.DriverAccounts.Actors.Tests;

/// <summary>Every token command and question reaches its token; nothing else is routed.</summary>
public sealed class DriverAccountsMessageExtractorsTests
{
    public static TheoryData<string> Samples =>
        new(DriverAccountsSamples.All.Select(sample => sample.GetType().Name).Distinct());

    [Theory]
    [MemberData(nameof(Samples))]
    public void Given_a_message_then_only_commands_and_questions_reach_the_token(string type)
    {
        var message = DriverAccountsSamples.All.First(sample => sample.GetType().Name == type);

        DriverAccountsMessageExtractors.Tokens.EntityId(message).ShouldBe(message switch
        {
            IdTokenCommand command => command.TokenUid.Value,
            IdTokenQuery query => query.TokenUid.Value,
            _ => null,
        });
    }
}
