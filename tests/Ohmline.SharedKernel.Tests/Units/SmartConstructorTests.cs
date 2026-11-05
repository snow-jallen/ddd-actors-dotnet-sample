using System.Globalization;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SharedKernel.Tests.Units;

public sealed class SmartConstructorTests
{
    [Fact]
    public void Given_valid_amounts_when_trying_to_create_units_then_each_is_ok()
    {
        Energy.TryCreate(19.75m).ShouldBe(Result.Ok(new Energy(19.75m)));
        Power.TryCreate(0m).ShouldBe(Result.Ok(Power.Zero));
        Current.TryCreate(32m).ShouldBe(Result.Ok(new Current(32m)));
        Voltage.TryCreate(230m).ShouldBe(Result.Ok(new Voltage(230m)));
    }

    [Fact]
    public void Given_negative_amounts_when_trying_to_create_units_then_each_is_out_of_range()
    {
        Energy.TryCreate(-1m)
            .ShouldBe(Result.Fail<ValidationError>(new OutOfRange("Energy", -1m, "0 kWh or more")));
        Power.TryCreate(-1m)
            .ShouldBe(Result.Fail<ValidationError>(new OutOfRange("Power", -1m, "0 kW or more")));
        Current.TryCreate(-1m)
            .ShouldBe(Result.Fail<ValidationError>(new OutOfRange("Current", -1m, "0 A or more")));
        Voltage.TryCreate(-1m)
            .ShouldBe(Result.Fail<ValidationError>(new OutOfRange("Voltage", -1m, "0 V or more")));
    }

    [Fact]
    public void Given_a_unit_when_used_through_the_contract_then_its_value_is_the_raw_amount()
    {
        var power = Create<Power, decimal>(22m);

        power.ShouldBe(new Power(22m));
        RawValue<Power, decimal>(power).ShouldBe(22m);
        Should.Throw<ArgumentException>(() => Create<Energy, decimal>(-1m))
            .Message.ShouldStartWith("Energy must be 0 kWh or more, not -1.");
    }

    [Fact]
    public void Given_supply_figures_when_chained_with_bind_then_the_result_is_power()
    {
        var power = Supply(volts: 230m, amperes: 32m);

        power.ShouldBe(Result.Ok(new Power(7.36m)));
    }

    [Fact]
    public void Given_an_invalid_first_figure_when_chained_then_the_later_steps_never_run()
    {
        var power = Supply(volts: -230m, amperes: -32m);

        power.ShouldBe(
            Result.Fail<ValidationError>(new OutOfRange("Voltage", -230m, "0 V or more")));
    }

    [Theory]
    [InlineData("22", "ok")]
    [InlineData(null, "missing")]
    [InlineData("  ", "missing")]
    [InlineData("22 kW", "malformed")]
    [InlineData("-5", "out of range")]
    public void Given_text_when_parsed_by_a_chain_then_each_error_case_can_come_out(
        string? text, string outcome)
    {
        var result = ParseKilowatts(text);

        result.Match(ok: _ => "ok", err: Describe).ShouldBe(outcome);
    }

    private static Result<Power, ValidationError> Supply(decimal volts, decimal amperes) =>
        Voltage.TryCreate(volts)
            .Bind(voltage => Current.TryCreate(amperes).Map(current => voltage * current));

    private static Result<Power, ValidationError> ParseKilowatts(string? text) =>
        Present("Power", text).Bind(ParseNumber).Bind(Power.TryCreate);

    private static Result<string, ValidationError> Present(string name, string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? Result.Fail<ValidationError>(new Missing(name))
            : Result.Ok(text.Trim());

    private static Result<decimal, ValidationError> ParseNumber(string text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? Result.Ok(number)
            : Result.Fail<ValidationError>(new Malformed("Power", text, "a number of kilowatts"));

    private static string Describe(ValidationError error) => error switch
    {
        OutOfRange => "out of range",
        Malformed => "malformed",
        Missing => "missing",
        null => "no error",
    };

    private static TSelf Create<TSelf, TValue>(TValue value)
        where TSelf : IValueObject<TSelf, TValue> => TSelf.Create(value);

    private static TValue RawValue<TSelf, TValue>(TSelf valueObject)
        where TSelf : IValueObject<TSelf, TValue> => valueObject.Value;
}
