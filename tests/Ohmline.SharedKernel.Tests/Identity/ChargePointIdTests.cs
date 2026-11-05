using Ohmline.SharedKernel.Identity;

namespace Ohmline.SharedKernel.Tests.Identity;

public sealed class ChargePointIdTests
{
    [Theory]
    [InlineData("OHM-SLC-0042")]
    [InlineData(" ohm-slc-0042 ")]
    public void Given_a_charge_point_key_when_trying_to_create_then_it_is_upper_case(string text)
    {
        ChargePointId.TryCreate(text).ShouldBe(Result.Ok(new ChargePointId("OHM-SLC-0042")));
    }

    [Theory]
    [InlineData("OHMSLC0042")]
    [InlineData("OHM-SLC-42")]
    [InlineData("OHM-SALT-0042")]
    [InlineData("XYZ-SLC-0042")]
    [InlineData("OHM-SLC-00٤2")]
    public void Given_text_that_is_no_charge_point_key_when_trying_to_create_then_it_is_malformed(
        string text)
    {
        ChargePointId.TryCreate(text).ShouldBe(Result.Fail<ValidationError>(new Malformed(
            "ChargePointId", text,
            "OHM-, an area code, a hyphen and four digits, like OHM-SLC-0042")));
    }

    [Fact]
    public void Given_no_text_when_trying_to_create_then_it_is_missing()
    {
        ChargePointId.TryCreate("  ")
            .ShouldBe(Result.Fail<ValidationError>(new Missing("ChargePointId")));
    }

    [Fact]
    public void Given_a_charge_point_id_when_printed_then_it_is_the_key_itself()
    {
        new ChargePointId("OHM-SLC-0042").ToString().ShouldBe("OHM-SLC-0042");
    }

    [Fact]
    public void Given_a_default_charge_point_id_then_its_value_is_null()
    {
        default(ChargePointId).Value.ShouldBeNull();
    }
}
