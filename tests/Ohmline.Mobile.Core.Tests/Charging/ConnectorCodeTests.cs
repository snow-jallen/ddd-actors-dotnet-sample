using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Core.Tests.Charging;

public sealed class ConnectorCodeTests
{
    public static TheoryData<string, string> Printed => new()
    {
        { "OHM-SLC-0042/1", "OHM-SLC-0042/1" },
        { " ohm-slc-0042/2 ", "OHM-SLC-0042/2" },
        { "OHM-DEN-1207/12", "OHM-DEN-1207/12" },
    };

    private const string LooksLike = "is not a connector code. It looks like OHM-SLC-0042/1.";

    public static TheoryData<string?, string> NotACode => new()
    {
        { null, "Scan or type the code on the connector's label." },
        { "  ", "Scan or type the code on the connector's label." },
        { "OHM-SLC-0042", $"'OHM-SLC-0042' {LooksLike}" },
        { "OHM-SLC-42/1", $"'OHM-SLC-42/1' {LooksLike}" },
        { "OHM-SLC-0042/0", $"'OHM-SLC-0042/0' {LooksLike}" },
        { " SLC-0042/1", $"'SLC-0042/1' {LooksLike}" },
    };

    [Theory]
    [MemberData(nameof(Printed))]
    public void Given_a_printed_code_then_it_reads_as_the_label_shows_it(string text, string code)
    {
        var parsed = ConnectorCode.TryParse(text).Value.ShouldBeOfType<Parsed<ConnectorCode>>();

        parsed.Value.ToString().ShouldBe(code);
    }

    [Theory]
    [MemberData(nameof(NotACode))]
    public void Given_text_that_is_not_a_code_then_the_driver_is_told_what_one_looks_like(
        string? text, string message)
    {
        ConnectorCode.TryParse(text).Value.ShouldBe(new Invalid(message));
    }

    [Fact]
    public void A_code_names_its_charge_point_and_its_connector()
    {
        var code = ConnectorCode.TryParse("OHM-SLC-0042/2").Value
            .ShouldBeOfType<Parsed<ConnectorCode>>().Value;

        (code.ChargePointId, code.Number).ShouldBe(("OHM-SLC-0042", 2));
    }
}
