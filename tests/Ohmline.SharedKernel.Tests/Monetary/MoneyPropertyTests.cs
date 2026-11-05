using CsCheck;
using Ohmline.SharedKernel.Monetary;
using static Ohmline.SharedKernel.Tests.Generators;
using Gen = CsCheck.Gen;

namespace Ohmline.SharedKernel.Tests.Monetary;

public sealed class MoneyPropertyTests
{
    [Fact]
    public void Adding_money_in_one_currency_is_associative_and_commutative()
    {
        Currencies
            .SelectMany(currency =>
                Gen.Select(AmountsIn(currency), AmountsIn(currency), AmountsIn(currency)))
            .Sample((a, b, c) =>
            {
                ((a + b) + c).ShouldBe(a + (b + c));
                (a + b).ShouldBe(b + a);
            });
    }

    [Fact]
    public void Trying_to_add_agrees_with_the_operator_within_one_currency()
    {
        Currencies
            .SelectMany(currency => Gen.Select(AmountsIn(currency), AmountsIn(currency)))
            .Sample((a, b) => a.TryAdd(b).ShouldBe(Result.Ok(a + b)));
    }

    [Fact]
    public void Trying_to_add_two_currencies_always_reports_the_mismatch()
    {
        Gen.Select(Amounts, Amounts)
            .Where((a, b) => a.Currency != b.Currency)
            .Sample((a, b) =>
                a.TryAdd(b).ShouldBe(Result.Fail(new CurrencyMismatch(a.Currency, b.Currency))));
    }

    [Fact]
    public void Rounding_moves_an_amount_by_less_than_one_step_and_half_a_step_at_midpoints()
    {
        Gen.Select(Amounts, RoundingPolicies).Sample((money, policy) =>
        {
            var step = new decimal(1, 0, 0, false, (byte)policy.Decimals);
            var limit = policy.Mode is MidpointRounding.ToEven or MidpointRounding.AwayFromZero
                ? step / 2
                : step;

            var error = Math.Abs(policy.Round(money).Amount - money.Amount);

            error.ShouldBeLessThanOrEqualTo(limit);
        });
    }

    [Fact]
    public void Rounding_twice_is_rounding_once_and_keeps_the_currency()
    {
        Gen.Select(Amounts, RoundingPolicies).Sample((money, policy) =>
        {
            var rounded = policy.Round(money);

            policy.Round(rounded).ShouldBe(rounded);
            rounded.Currency.ShouldBe(money.Currency);
        });
    }
}
