using Ohmline.SharedKernel.Monetary;

namespace Ohmline.SharedKernel.Tests.Monetary;

public sealed class CurrencyTests
{
    [Fact]
    public void Given_an_iso_code_when_trying_to_create_then_it_is_a_currency()
    {
        Currency.TryCreate("USD").ShouldBe(Result.Ok(Currency.Usd));
    }

    [Fact]
    public void Given_a_code_as_people_type_it_when_trying_to_create_then_it_is_made_canonical()
    {
        Currency.TryCreate(" eur ").ShouldBe(Result.Ok(new Currency("EUR")));
    }

    [Theory]
    [InlineData("US$")]
    [InlineData("USDX")]
    [InlineData("U5D")]
    public void Given_text_that_is_no_iso_code_when_trying_to_create_then_it_is_malformed(
        string code)
    {
        Currency.TryCreate(code).ShouldBe(Result.Fail<ValidationError>(
            new Malformed("Currency", code, "an ISO 4217 code such as USD")));
    }

    [Fact]
    public void Given_no_code_when_trying_to_create_then_it_is_missing()
    {
        Currency.TryCreate(" ").ShouldBe(Result.Fail<ValidationError>(new Missing("Currency")));
    }

    [Fact]
    public void Given_a_code_that_is_not_canonical_when_constructed_then_the_guard_throws()
    {
        Should.Throw<ArgumentException>(() => new Currency("usd")).ParamName.ShouldBe("Code");
    }
}
