using Ohmline.SharedKernel.Monetary;

namespace Ohmline.SharedKernel.Tests.Monetary;

public sealed class RoundingPolicyTests
{
    // Decimal theory data comes from TheoryData: attribute arguments cannot be decimals, and
    // InlineData(8.285) would arrive as the double nearest to it, 8.28500000000000014...
    public static TheoryData<decimal, decimal> CentRoundings => new()
    {
        { 8.29626m, 8.30m },
        { 8.285m, 8.29m },
        { -8.285m, -8.29m },
        { 8.2849m, 8.28m },
    };

    [Theory]
    [MemberData(nameof(CentRoundings))]
    public void Given_an_amount_when_rounded_to_cents_then_halves_go_away_from_zero(
        decimal amount, decimal rounded)
    {
        var money = new Money(amount, Currency.Usd);

        RoundingPolicy.Cents.Round(money).ShouldBe(new Money(rounded, Currency.Usd));
    }

    [Fact]
    public void Given_two_policies_when_a_half_cent_is_rounded_then_they_disagree()
    {
        var money = new Money(8.285m, Currency.Usd);
        var bankers = new RoundingPolicy(2, MidpointRounding.ToEven);

        RoundingPolicy.Cents.Round(money).Amount.ShouldBe(8.29m);
        bankers.Round(money).Amount.ShouldBe(8.28m);
    }

    [Fact]
    public void Given_a_currency_without_minor_units_when_rounded_then_whole_amounts_remain()
    {
        var yen = new Money(1234.5m, new Currency("JPY"));

        new RoundingPolicy(0, MidpointRounding.AwayFromZero).Round(yen).Amount.ShouldBe(1235m);
    }

    [Fact]
    public void Given_a_policy_that_always_rounds_down_then_fractions_never_go_up()
    {
        var policy = new RoundingPolicy(2, MidpointRounding.ToZero);

        policy.Round(new Money(8.299m, Currency.Usd)).Amount.ShouldBe(8.29m);
    }

    [Fact]
    public void Given_valid_settings_when_trying_to_create_then_it_is_a_policy()
    {
        RoundingPolicy.TryCreate(2, MidpointRounding.AwayFromZero)
            .ShouldBe(Result.Ok(RoundingPolicy.Cents));
    }

    [Fact]
    public void Given_too_many_decimals_when_trying_to_create_then_it_is_out_of_range()
    {
        RoundingPolicy.TryCreate(29, MidpointRounding.ToEven).ShouldBe(
            Result.Fail<ValidationError>(new OutOfRange("Rounding decimals", 29, "0 to 28")));
    }

    [Fact]
    public void Given_an_unknown_mode_when_trying_to_create_then_it_is_out_of_range()
    {
        RoundingPolicy.TryCreate(2, (MidpointRounding)42).ShouldBe(Result.Fail<ValidationError>(
            new OutOfRange("Rounding mode", 42, "a MidpointRounding value")));
    }

    [Fact]
    public void Given_invalid_settings_when_constructed_then_the_guard_throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new RoundingPolicy(-1, default));
        Should.Throw<ArgumentOutOfRangeException>(
            () => RoundingPolicy.Cents with { Mode = (MidpointRounding)42 });
    }
}
