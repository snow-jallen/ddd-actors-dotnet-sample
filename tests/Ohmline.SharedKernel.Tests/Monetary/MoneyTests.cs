using Ohmline.SharedKernel.Monetary;

namespace Ohmline.SharedKernel.Tests.Monetary;

public sealed class MoneyTests
{
    private static readonly Money FiveDollars = new(5.00m, Currency.Usd);
    private static readonly Money ThreeEuros = new(3.00m, Currency.Eur);

    [Fact]
    public void Given_two_amounts_in_one_currency_when_added_then_the_sum_keeps_the_currency()
    {
        (FiveDollars + new Money(2.50m, Currency.Usd)).ShouldBe(new Money(7.50m, Currency.Usd));
        (FiveDollars - new Money(7.50m, Currency.Usd)).ShouldBe(new Money(-2.50m, Currency.Usd));
    }

    [Fact]
    public void Given_two_amounts_in_one_currency_when_tried_then_the_sum_is_ok()
    {
        FiveDollars.TryAdd(FiveDollars).ShouldBe(Result.Ok(new Money(10m, Currency.Usd)));
    }

    [Fact]
    public void Given_amounts_in_two_currencies_when_tried_then_the_result_is_a_mismatch()
    {
        var result = FiveDollars.TryAdd(ThreeEuros);

        result.ShouldBe(Result.Fail(new CurrencyMismatch(Currency.Usd, Currency.Eur)));
        result.Match(ok: _ => "", err: mismatch => mismatch.Message)
            .ShouldBe("Cannot combine USD with EUR: convert one of them first.");
    }

    [Fact]
    public void Given_amounts_in_two_currencies_when_an_operator_is_used_then_it_throws()
    {
        Should.Throw<InvalidOperationException>(() => FiveDollars + ThreeEuros);
        Should.Throw<InvalidOperationException>(() => FiveDollars - ThreeEuros);
        Should.Throw<InvalidOperationException>(() => FiveDollars < ThreeEuros);
    }

    [Fact]
    public void Given_amounts_in_two_currencies_then_they_are_never_equal()
    {
        new Money(3m, Currency.Usd).ShouldNotBe(new Money(3m, Currency.Eur));
    }

    [Fact]
    public void Given_amounts_in_one_currency_when_compared_then_the_amounts_decide()
    {
        (FiveDollars > new Money(4.99m, Currency.Usd)).ShouldBeTrue();
        (FiveDollars <= new Money(5m, Currency.Usd)).ShouldBeTrue();
    }

    [Fact]
    public void Given_a_price_per_kwh_when_scaled_by_the_energy_then_nothing_is_rounded_yet()
    {
        var price = new Money(0.42m, Currency.Usd) * 19.753m;

        price.ShouldBe(new Money(8.29626m, Currency.Usd));
    }

    [Fact]
    public void Given_money_without_a_currency_when_constructed_then_the_guard_throws()
    {
        Should.Throw<ArgumentException>(() => new Money(1m, default)).ParamName
            .ShouldBe("Currency");
    }

    [Fact]
    public void Given_money_when_printed_then_it_shows_amount_and_currency()
    {
        new Money(8.30m, Currency.Usd).ToString().ShouldBe("8.30 USD");
        Money.Zero(Currency.Eur).ToString().ShouldBe("0 EUR");
    }
}
