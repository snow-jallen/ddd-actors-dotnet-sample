namespace Ch03.LanguageLab.Tests;

public sealed class FieldKeywordTests
{
    [Fact]
    public void Given_a_non_negative_value_when_constructed_then_it_is_kept()
    {
        var power = new Kilowatts(22m);

        power.Value.ShouldBe(22m);
        power.ToString().ShouldBe("22 kW");
    }

    [Fact]
    public void Given_a_negative_value_when_constructed_then_the_guard_throws()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new Kilowatts(-1m));

        exception.ParamName.ShouldBe("Value");
        exception.ActualValue.ShouldBe(-1m);
    }

    [Fact]
    public void Given_a_valid_value_when_copied_with_a_valid_change_then_the_copy_has_it()
    {
        var power = new Kilowatts(22m);

        var reduced = power with { Value = 11m };

        reduced.ShouldBe(new Kilowatts(11m));
    }

    [Fact]
    public void Given_a_valid_value_when_copied_with_a_negative_change_then_the_guard_throws()
    {
        var power = new Kilowatts(22m);

        Should.Throw<ArgumentOutOfRangeException>(() => power with { Value = -5m });
    }

    [Fact]
    public void Given_a_guard_in_the_initializer_when_copied_with_then_the_guard_is_skipped()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new InitializerGuardedKilowatts(-1m));

        var power = new InitializerGuardedKilowatts(22m);
        var invalid = power with { Value = -5m };

        invalid.Value.ShouldBe(-5m);
    }

    [Fact]
    public void Given_a_default_kilowatts_then_no_constructor_or_init_accessor_ran()
    {
        Kilowatts[] slots = new Kilowatts[2];

        default(Kilowatts).Value.ShouldBe(0m);
        new Kilowatts().Value.ShouldBe(0m);
        slots.ShouldAllBe(slot => slot.Value == 0m);
    }

    [Fact]
    public void Given_a_default_connector_number_then_it_holds_a_value_the_guard_rejects()
    {
        var unset = default(ConnectorNumber);

        unset.Value.ShouldBe(0);
        Should.Throw<ArgumentOutOfRangeException>(() => new ConnectorNumber(0));
    }
}
