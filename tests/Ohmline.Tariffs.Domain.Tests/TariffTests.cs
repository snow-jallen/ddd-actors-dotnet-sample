using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;
using static Ohmline.Tariffs.Domain.Tests.TheDepotTariff;

namespace Ohmline.Tariffs.Domain.Tests;

/// <summary>
/// A tariff is data with rules about its shape: the pricing analyst can publish any list of
/// components, as long as every moment has one rate of a kind and every amount is in the
/// tariff's currency.
/// </summary>
public sealed class TariffTests
{
    [Fact]
    public void Given_the_primers_rules_then_they_are_a_tariff()
    {
        var tariff = Version1();

        tariff.Version.ShouldBe(1);
        tariff.Components.ShouldBe([BaseRate, Fee, Idle]);
    }

    [Fact]
    public void Given_peak_and_night_bands_over_a_base_rate_then_they_are_a_tariff()
    {
        Create(
                new EnergyRate(Usd(0.42m)),
                new EnergyRate(Usd(0.60m), Peak),
                new EnergyRate(Usd(0.25m), Night),
                new TimeRate(Usd(0.05m), Peak))
            .IsOk.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_band_that_ends_where_the_next_starts_then_they_do_not_overlap()
    {
        var morning = new TimeOfUseBand("morning", new TimeOnly(6, 0), new TimeOnly(9, 0));

        Create(new EnergyRate(Usd(0.25m), Night), new EnergyRate(Usd(0.50m), morning))
            .IsOk.ShouldBeTrue();
    }

    [Fact]
    public void Given_an_amount_in_another_currency_then_it_is_not_a_tariff()
    {
        Create(BaseRate, new SessionFee(new Money(1.00m, Currency.Eur))).ShouldBe(Invalid(
            new Malformed("Session fee currency", "EUR", "USD, the tariff's")));
    }

    [Fact]
    public void Given_an_idle_fee_cap_in_another_currency_then_it_is_not_a_tariff()
    {
        Create(BaseRate, Idle with { Cap = new Money(5.00m, Currency.Eur) }).ShouldBe(Invalid(
            new Malformed("Idle fee cap currency", "EUR", "USD, the tariff's")));
    }

    [Fact]
    public void Given_a_negative_price_then_it_is_not_a_tariff()
    {
        Create(new EnergyRate(Usd(-0.10m))).ShouldBe(Invalid(
            new OutOfRange("Energy rate", -0.10m, "0 or more")));
    }

    [Fact]
    public void Given_two_session_fees_then_it_is_not_a_tariff()
    {
        Create(BaseRate, Fee, new SessionFee(Usd(2.00m))).ShouldBe(Invalid(
            new Malformed("Components", "more than one session fee", "at most one session fee")));
    }

    [Fact]
    public void Given_two_energy_rates_without_a_band_then_it_is_not_a_tariff()
    {
        Create(BaseRate, new EnergyRate(Usd(0.50m))).ShouldBe(Invalid(new Malformed(
            "energy rates", "two rates without a band", "at most one rate without a band")));
    }

    [Fact]
    public void Given_overlapping_bands_of_one_kind_then_it_is_not_a_tariff()
    {
        var evening = new TimeOfUseBand("evening", new TimeOnly(20, 0), new TimeOnly(23, 0));

        Create(new EnergyRate(Usd(0.60m), Peak), new EnergyRate(Usd(0.50m), evening))
            .ShouldBe(Invalid(new Malformed(
                "energy rate bands",
                "peak (16:00 to 21:00) and evening (20:00 to 23:00)",
                "bands that do not overlap")));
    }

    [Fact]
    public void Given_overlapping_bands_of_different_kinds_then_they_are_a_tariff()
    {
        Create(new EnergyRate(Usd(0.60m), Peak), new TimeRate(Usd(0.05m), Peak))
            .IsOk.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_negative_grace_period_then_it_is_not_a_tariff()
    {
        Create(BaseRate, Idle with { GracePeriod = TimeSpan.FromMinutes(-5) }).ShouldBe(Invalid(
            new OutOfRange("Grace period", -5m, "0 minutes or more")));
    }

    [Theory]
    [InlineData("", "Time zone is missing.")]
    [InlineData("Mountain Time", "Time zone must be an IANA time zone like America/Denver, "
        + "not 'Mountain Time'.")]
    public void Given_no_usable_time_zone_then_it_is_not_a_tariff(string zone, string message)
    {
        var tariff = Tariff.TryCreate(
            Id, 1, Currency.Usd, Components, null, RoundingPolicy.Cents, zone);

        tariff.Match(_ => "valid", error => error.Message).ShouldBe(message);
    }

    [Fact]
    public void Given_version_zero_then_it_is_not_a_tariff()
    {
        Tariff.TryCreate(Id, 0, Currency.Usd, Components, null, RoundingPolicy.Cents,
                "America/Denver")
            .ShouldBe(Invalid(new OutOfRange("Tariff version", 0, "1 or more")));
    }

    [Fact]
    public void Given_a_price_cap_in_another_currency_then_it_is_not_a_tariff()
    {
        Tariff.TryCreate(Id, 1, Currency.Usd, Components, new Money(50m, Currency.Eur),
                RoundingPolicy.Cents, "America/Denver")
            .ShouldBe(Invalid(new Malformed("Price cap currency", "EUR", "USD, the tariff's")));
    }

    [Fact]
    public void Given_a_tariff_when_revised_then_the_next_version_has_the_new_rules()
    {
        var first = Version1();
        var dearer = new EnergyRate(Usd(0.45m));

        var second = first.Revise([dearer, Fee, Idle], priceCap: Usd(60m))
            .Match(valid => valid, error => throw new ShouldAssertException(error.Message));

        second.Version.ShouldBe(2);
        second.Components.ShouldBe([dearer, Fee, Idle]);
        second.PriceCap.ShouldBe(Usd(60m));
        first.ShouldBe(Version1());
    }

    [Fact]
    public void Given_a_snapshot_taken_before_a_revision_then_the_revision_does_not_touch_it()
    {
        var first = Version1();
        var snapshot = first.Snapshot();

        first.Revise([new EnergyRate(Usd(0.45m))], priceCap: null);

        snapshot.ShouldBe(Version1().Snapshot());
        snapshot.Version.ShouldBe(1);
        snapshot.Components.ShouldBe([BaseRate, Fee, Idle]);
    }

    [Fact]
    public void Given_two_snapshots_of_the_same_version_then_they_are_equal()
    {
        var snapshot = Version1().Snapshot();
        var copy = snapshot with { Components = [.. snapshot.Components] };

        copy.ShouldBe(snapshot);
        copy.GetHashCode().ShouldBe(snapshot.GetHashCode());
        (snapshot with { Version = 2 }).ShouldNotBe(snapshot);
    }

    private static Result<Tariff, ValidationError> Create(params TariffComponent[] components) =>
        Tariff.TryCreate(
            Id, 1, Currency.Usd, components, null, RoundingPolicy.Cents, "America/Denver");

    private static Result<Tariff, ValidationError> Invalid(ValidationError error) =>
        Result.Fail(error);
}
