namespace Ch03.LanguageLab.Tests;

public sealed class RecordEqualityTests
{
    [Fact]
    public void Given_two_record_classes_with_equal_members_then_they_are_equal()
    {
        var first = new ChargerInfo("OHM-SLC-0042", "Voltaro", "VX-22");
        var second = new ChargerInfo("OHM-SLC-0042", "Voltaro", "VX-22");

        first.ShouldBe(second);
        (first == second).ShouldBeTrue();
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void Given_a_record_when_copied_with_a_change_then_the_original_is_untouched()
    {
        var installed = new ChargerInfo("OHM-SLC-0042", "Voltaro", "VX-22");

        var replaced = installed with { Model = "VX-50" };

        replaced.ShouldBe(new ChargerInfo("OHM-SLC-0042", "Voltaro", "VX-50"));
        installed.Model.ShouldBe("VX-22");
    }

    [Fact]
    public void Given_a_record_when_printed_then_every_member_is_shown()
    {
        var charger = new ChargerInfo("OHM-SLC-0042", "Voltaro", "VX-22");

        charger.ToString().ShouldBe(
            "ChargerInfo { ChargePointId = OHM-SLC-0042, Vendor = Voltaro, Model = VX-22 }");
    }

    [Fact]
    public void Given_a_record_class_when_assigned_then_both_names_share_one_object()
    {
        var charger = new ChargerInfo("OHM-SLC-0042", "Voltaro", "VX-22");

        var alias = charger;

        alias.ShouldBeSameAs(charger);
    }

    [Fact]
    public void Given_a_record_struct_when_assigned_then_the_value_is_copied()
    {
        var connector = new ConnectorKey("OHM-SLC-0042", 1);

        object first = connector;
        object second = connector;

        first.ShouldBe(second);
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void Given_a_record_struct_when_copied_with_a_change_then_it_is_a_new_value()
    {
        var first = new ConnectorKey("OHM-SLC-0042", 1);

        var second = first with { Number = 2 };

        second.ShouldBe(new ConnectorKey("OHM-SLC-0042", 2));
        first.Number.ShouldBe(1);
    }

    [Fact]
    public void Given_a_default_record_struct_then_it_exists_without_any_constructor_running()
    {
        var empty = default(ConnectorKey);

        empty.ChargePointId.ShouldBeNull();
        empty.Number.ShouldBe(0);
    }

    [Fact]
    public void Given_records_with_equal_immutable_arrays_then_they_are_not_equal()
    {
        var first = new TariffRates("standard", [0.42m, 0.30m]);
        var second = new TariffRates("standard", [0.42m, 0.30m]);

        // ImmutableArray<T> compares by reference, so the generated Equals does too.
        first.ShouldNotBe(second);
        first.PricePerKwh.SequenceEqual(second.PricePerKwh).ShouldBeTrue();
    }
}
