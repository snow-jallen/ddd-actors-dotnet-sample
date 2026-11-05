using Ohmline.SharedKernel.Identity;

namespace Ohmline.SharedKernel.Tests.Identity;

public sealed class TokenUidTests
{
    [Theory]
    [InlineData("04A2B3C4D5E6F7")]
    [InlineData("04a2b3c4d5e6f7")]
    [InlineData(" 04A2b3C4d5E6f7 ")]
    public void Given_a_card_uid_in_any_case_when_trying_to_create_then_it_is_upper_case(
        string text)
    {
        TokenUid.TryCreate(text).ShouldBe(Result.Ok(new TokenUid("04A2B3C4D5E6F7")));
    }

    [Theory]
    [InlineData("04A1B2C3")]
    [InlineData("04A1B2C3D4E5F6071829")]
    public void Given_four_or_ten_bytes_when_trying_to_create_then_both_lengths_are_accepted(
        string text)
    {
        TokenUid.TryCreate(text).IsOk.ShouldBeTrue();
    }

    [Theory]
    [InlineData("04A1B2")]
    [InlineData("04A1B2C3D")]
    [InlineData("04A1B2C3D4E5F60718293A4B")]
    [InlineData("04:A1:B2:C3")]
    [InlineData("GGA1B2C3")]
    public void Given_text_that_is_no_card_uid_when_trying_to_create_then_it_is_malformed(
        string text)
    {
        TokenUid.TryCreate(text).ShouldBe(Result.Fail<ValidationError>(
            new Malformed("TokenUid", text, "4 to 10 bytes of hexadecimal, like 04A2B3C4")));
    }

    [Fact]
    public void Given_no_text_when_trying_to_create_then_it_is_missing()
    {
        TokenUid.TryCreate("").ShouldBe(Result.Fail<ValidationError>(new Missing("TokenUid")));
    }

    [Fact]
    public void Given_a_uid_in_lower_case_when_constructed_then_the_guard_throws()
    {
        Should.Throw<ArgumentException>(() => new TokenUid("04a1b2c3"));
    }
}
