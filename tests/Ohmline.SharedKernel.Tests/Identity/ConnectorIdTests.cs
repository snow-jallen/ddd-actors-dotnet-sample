using Ohmline.SharedKernel.Identity;

namespace Ohmline.SharedKernel.Tests.Identity;

public sealed class ConnectorIdTests
{
    private static readonly ChargePointId ChargePoint = new("OHM-SLC-0042");

    [Fact]
    public void Given_a_charge_point_and_a_number_then_the_connector_is_written_with_a_slash()
    {
        new ConnectorId(ChargePoint, 1).ToString().ShouldBe("OHM-SLC-0042/1");
    }

    [Fact]
    public void Given_the_same_number_on_two_charge_points_then_they_are_different_connectors()
    {
        new ConnectorId(ChargePoint, 1)
            .ShouldNotBe(new ConnectorId(new ChargePointId("OHM-SLC-0043"), 1));
        new ConnectorId(ChargePoint, 1).ShouldBe(new ConnectorId(ChargePoint, 1));
    }

    [Fact]
    public void Given_connector_number_zero_when_trying_to_create_then_it_is_out_of_range()
    {
        ConnectorId.TryCreate(ChargePoint, 0).ShouldBe(
            Result.Fail<ValidationError>(new OutOfRange("Connector number", 0, "1 or more")));
    }

    [Fact]
    public void Given_the_written_form_when_trying_to_create_then_both_parts_are_read()
    {
        ConnectorId.TryCreate("ohm-slc-0042/2")
            .ShouldBe(Result.Ok(new ConnectorId(ChargePoint, 2)));
    }

    [Theory]
    [InlineData("OHM-SLC-0042")]
    [InlineData("OHM-SLC-0042/one")]
    [InlineData("OHM-SLC-0042/1/2")]
    public void Given_text_without_one_slash_and_a_number_then_it_is_malformed(string text)
    {
        ConnectorId.TryCreate(text).ShouldBe(Result.Fail<ValidationError>(
            new Malformed("ConnectorId", text, "a charge point id, /, a number")));
    }

    [Fact]
    public void Given_a_bad_charge_point_in_the_written_form_then_its_error_comes_through()
    {
        var result = ConnectorId.TryCreate("OHM-42/1");

        result.Match(ok: _ => "", err: error => error.Message)
            .ShouldStartWith("ChargePointId must be OHM-");
    }

    [Fact]
    public void Given_a_connector_id_through_the_contract_then_its_value_is_the_written_form()
    {
        RawValue<ConnectorId, string>(new ConnectorId(ChargePoint, 1)).ShouldBe("OHM-SLC-0042/1");
    }

    [Fact]
    public void Given_invalid_parts_when_constructed_then_the_guards_throw()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ConnectorId(ChargePoint, 0));
        Should.Throw<ArgumentException>(() => new ConnectorId(default, 1));
    }

    private static TValue RawValue<TSelf, TValue>(TSelf valueObject)
        where TSelf : IValueObject<TSelf, TValue> => valueObject.Value;
}
