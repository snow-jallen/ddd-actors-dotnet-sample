using Ohmline.Charging.Client.Contracts;
using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Core.Tests.Charging;

public sealed class RejectionTextTests
{
    public static TheoryData<string> Codes => new(RejectionCodes.All);

    [Theory]
    [MemberData(nameof(Codes))]
    public void Every_published_code_has_a_sentence_of_its_own(string code)
    {
        RejectionText.For(code).ShouldNotBe(RejectionText.Fallback);
    }

    [Fact]
    public void No_two_codes_read_the_same()
    {
        RejectionCodes.All.Select(RejectionText.For).ShouldBeUnique();
    }

    [Fact]
    public void A_reserved_connector_is_explained_in_the_drivers_words()
    {
        RejectionText.For(RejectionCodes.ReservedForAnotherToken)
            .ShouldBe("This connector is reserved for another driver.");
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("")]
    [InlineData(null)]
    public void Given_a_code_this_build_does_not_know_then_a_general_sentence(string? code)
    {
        RejectionText.For(code).ShouldBe(RejectionText.Fallback);
    }
}
