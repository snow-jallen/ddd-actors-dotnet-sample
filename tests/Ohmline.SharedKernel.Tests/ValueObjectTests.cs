namespace Ohmline.SharedKernel.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Given_a_valid_raw_value_when_trying_to_create_then_it_is_ok()
    {
        var result = StateOfCharge.TryCreate(80);

        result.Match(ok: soc => soc.Value, err: _ => -1).ShouldBe(80);
    }

    [Fact]
    public void Given_an_invalid_raw_value_when_trying_to_create_then_it_says_why()
    {
        var result = StateOfCharge.TryCreate(120);

        result.ShouldBe(
            Result.Fail<ValidationError>(new OutOfRange("State of charge", 120, "0 to 100 %")));
    }

    [Fact]
    public void Given_a_valid_raw_value_when_created_through_the_contract_then_it_is_returned()
    {
        Create<StateOfCharge, int>(80).Value.ShouldBe(80);
    }

    [Fact]
    public void Given_an_invalid_raw_value_when_created_through_the_contract_then_it_throws()
    {
        var exception = Should.Throw<ArgumentException>(() => Create<StateOfCharge, int>(-1));

        exception.Message.ShouldStartWith("State of charge must be 0 to 100 %, not -1.");
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Given_any_value_object_then_generic_code_can_create_it_from_its_raw_value()
    {
        CreateAll<StateOfCharge, int>([20, 80]).Select(soc => soc.Value).ShouldBe([20, 80]);
    }

    // A static virtual member is called through a type parameter, never through the
    // implementing type: StateOfCharge.Create(80) does not compile.
    private static TSelf Create<TSelf, TValue>(TValue value)
        where TSelf : IValueObject<TSelf, TValue> => TSelf.Create(value);

    private static IEnumerable<TSelf> CreateAll<TSelf, TValue>(IEnumerable<TValue> values)
        where TSelf : IValueObject<TSelf, TValue> => values.Select(TSelf.Create);
}
