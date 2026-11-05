using System.Globalization;
using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Core.Tests.Charging;

public sealed class SpendCapTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static TheoryData<string, decimal> WithinRange => new()
    {
        { "40", 40m },
        { "1", 1m },
        { "500", 500m },
        { "12.50", 12.50m },
    };

    public static TheoryData<string?, string> NotASpendCap => new()
    {
        { null, "Choose a spend cap from 1 to 500 USD." },
        { "forty", "Choose a spend cap from 1 to 500 USD." },
        { "0", "A spend cap is from 1 to 500 USD, not 0." },
        { "500.01", "A spend cap is from 1 to 500 USD, not 500.01." },
        { "12.345", "A spend cap is in whole cents." },
    };

    [Theory]
    [MemberData(nameof(WithinRange))]
    public void Given_an_amount_within_range_then_it_is_a_spend_cap(string text, decimal amount)
    {
        SpendCap.TryParse(text, English).Value.ShouldBeOfType<Parsed<SpendCap>>()
            .Value.Amount.ShouldBe(amount);
    }

    [Theory]
    [MemberData(nameof(NotASpendCap))]
    public void Given_an_amount_out_of_range_then_the_driver_is_told_the_range(
        string? text, string message)
    {
        SpendCap.TryParse(text, English).Value.ShouldBe(new Invalid(message));
    }

    [Fact]
    public void Given_a_phone_in_german_then_a_comma_is_the_decimal_separator()
    {
        SpendCap.TryParse("12,50", CultureInfo.GetCultureInfo("de-DE")).Value
            .ShouldBeOfType<Parsed<SpendCap>>().Value.Amount.ShouldBe(12.50m);
    }
}
