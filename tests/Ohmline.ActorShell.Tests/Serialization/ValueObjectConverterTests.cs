using System.Text.Json;
using Ohmline.ActorShell.Serialization;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.ActorShell.Tests.Serialization;

public sealed class ValueObjectConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new ValueObjectConverter() },
    };

    [Fact]
    public void Given_identifiers_when_written_then_each_is_its_printed_form()
    {
        var chargePoint = new ChargePointId("OHM-SLC-0042");

        Write(chargePoint).ShouldBe("\"OHM-SLC-0042\"");
        Write(new ConnectorId(chargePoint, 1)).ShouldBe("\"OHM-SLC-0042/1\"");
        Write(new SiteId("slc-depot")).ShouldBe("\"slc-depot\"");
        Write(new CommandId(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042")))
            .ShouldBe("\"019a4f6e-1c00-7d2a-9b3e-5c0a00000042\"");
    }

    [Fact]
    public void Given_units_and_money_when_written_then_numbers_and_codes_are_bare()
    {
        Write(new Power(7.4m)).ShouldBe("7.4");
        Write(new Energy(12.345m)).ShouldBe("12.345");
        Write(new Money(0.35m, Currency.Usd)).ShouldBe("""{"Amount":0.35,"Currency":"USD"}""");
    }

    [Fact]
    public void Given_written_value_objects_when_read_then_they_are_equal_again()
    {
        var connector = new ConnectorId(new ChargePointId("OHM-SLC-0042"), 2);
        var money = new Money(12.5m, Currency.Eur);

        Read<ConnectorId>(Write(connector)).ShouldBe(connector);
        Read<Power>(Write(new Power(22m))).ShouldBe(new Power(22m));
        Read<Money>(Write(money)).ShouldBe(money);
    }

    [Fact]
    public void Given_an_optional_value_object_when_absent_then_it_is_written_and_read_as_null()
    {
        Write<SiteId?>(null).ShouldBe("null");
        Read<SiteId?>("null").ShouldBeNull();
        Read<SiteId?>("\"slc-depot\"").ShouldBe(new SiteId("slc-depot"));
    }

    [Fact]
    public void Given_an_invalid_raw_value_when_read_then_the_value_object_refuses_it()
    {
        Should.Throw<JsonException>(() => Read<ChargePointId>("\"OHM-42\""))
            .Message.ShouldStartWith("Not a valid ChargePointId");
        Should.Throw<JsonException>(() => Read<Power>("-1"))
            .Message.ShouldStartWith("Not a valid Power");
    }

    private static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}
