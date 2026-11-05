namespace Ohmline.SharedKernel.Tests;

public sealed class ValidationErrorTests
{
    [Fact]
    public void Given_a_number_out_of_range_then_the_message_names_the_allowed_values()
    {
        ValidationError error = new OutOfRange("Energy", -1.5m, "0 kWh or more");

        error.Message.ShouldBe("Energy must be 0 kWh or more, not -1.5.");
    }

    [Fact]
    public void Given_malformed_text_then_the_message_quotes_it_and_names_the_expected_shape()
    {
        ValidationError error = new Malformed("ChargePointId", "OHM-42", "like OHM-SLC-0042");

        error.Message.ShouldBe("ChargePointId must be like OHM-SLC-0042, not 'OHM-42'.");
    }

    [Fact]
    public void Given_a_missing_value_then_the_message_names_it()
    {
        ValidationError error = new Missing("TokenUid");

        error.Message.ShouldBe("TokenUid is missing.");
    }

    [Fact]
    public void Given_a_default_validation_error_then_the_message_says_it_holds_no_case()
    {
        default(ValidationError).Message
            .ShouldBe("This ValidationError is default: it holds no case.");
    }

    [Fact]
    public void Given_two_errors_holding_equal_cases_then_they_are_equal()
    {
        ValidationError first = new Missing("SiteId");
        ValidationError second = new Missing("SiteId");

        first.ShouldBe(second);
        first.ShouldNotBe(new Missing("TokenUid"));
    }
}
