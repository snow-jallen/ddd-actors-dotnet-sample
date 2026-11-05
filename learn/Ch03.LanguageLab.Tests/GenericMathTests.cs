namespace Ch03.LanguageLab.Tests;

public sealed class GenericMathTests
{
    [Fact]
    public void Given_power_allocations_when_summed_then_the_total_is_a_quantity_too()
    {
        Kilowatts[] allocations = [new(38m), new(38m), new(22m)];

        GenericMath.Sum(allocations).ShouldBe(new Kilowatts(98m));
    }

    [Fact]
    public void Given_no_allocations_when_summed_then_the_total_is_the_additive_identity()
    {
        GenericMath.Sum<Kilowatts>([]).ShouldBe(Kilowatts.AdditiveIdentity);
    }

    [Fact]
    public void Given_the_same_algorithm_when_used_with_built_in_numbers_then_it_works_too()
    {
        GenericMath.Sum([38m, 38m, 22m]).ShouldBe(98m);
        GenericMath.Sum([1, 2, 3]).ShouldBe(6);
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(38, 38)]
    [InlineData(80, 50)]
    public void Given_a_requested_power_when_clamped_to_the_connector_then_it_stays_within(
        int requestedKw, int grantedKw)
    {
        var granted = GenericMath.Clamp(
            new Kilowatts(requestedKw), min: new Kilowatts(5m), max: new Kilowatts(50m));

        granted.ShouldBe(new Kilowatts(grantedKw));
    }

    [Fact]
    public void Given_quantities_when_compared_then_the_operators_compare_their_values()
    {
        (new Kilowatts(11m) < new Kilowatts(22m)).ShouldBeTrue();
        (new Kilowatts(22m) >= new Kilowatts(22m)).ShouldBeTrue();
        (new Kilowatts(22m) == new Kilowatts(22m)).ShouldBeTrue();
    }
}
