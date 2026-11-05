using System.Text.Json;
using Ch03.ClosedLib;

namespace Ch03.LanguageLab.Tests;

public sealed class SerializationRealityTests
{
    [Fact]
    public void Given_a_union_when_written_then_only_the_case_is_on_the_wire()
    {
        var json = JsonSerializer.Serialize<ConnectorStatus>(new Occupied("session-a"));

        json.ShouldBe("""{"SessionId":"session-a"}""");
    }

    [Fact]
    public void Given_a_union_of_object_cases_when_read_back_then_it_is_ambiguous()
    {
        var exception = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<ConnectorStatus>("""{"SessionId":"session-a"}"""));

        exception.Message.ShouldContain("ambiguous");
    }

    [Fact]
    public void Given_a_union_of_different_token_kinds_when_read_back_then_each_case_returns()
    {
        RoundTrip<ConfigurationValue>(60).Value.ShouldBe(60);
        RoundTrip<ConfigurationValue>(true).Value.ShouldBe(true);
        RoundTrip<ConfigurationValue>("Voltaro").Value.ShouldBe("Voltaro");
    }

    [Fact]
    public void Given_a_structural_classifier_when_each_case_has_its_own_property_then_it_works()
    {
        RoundTrip<StartBlocker>(new Occupied("session-a")).Value.ShouldBeOfType<Occupied>();
        RoundTrip<StartBlocker>(new Faulted("E-42")).Value.ShouldBe(new Faulted("E-42"));
    }

    [Fact]
    public void Given_a_structural_classifier_when_no_property_matches_then_reading_fails()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<StartBlocker>("{}"));
    }

    [Fact]
    public void Given_a_structural_classifier_when_a_case_has_no_properties_then_it_is_refused()
    {
        var exception = Should.Throw<NotSupportedException>(
            () => JsonSerializer.Serialize<StructuralConnectorStatus>(new Occupied("session-a")));

        exception.Message.ShouldContain("'Ch03.LanguageLab.Tests.Available' can never be selected");
    }

    [Fact]
    public void Given_a_closed_record_without_polymorphism_when_written_then_leaf_fields_vanish()
    {
        ChargerCommand command = new RemoteStop("OHM-SLC-0042", "session-a");

        var json = JsonSerializer.Serialize(command);

        json.ShouldBe("""{"ChargePointId":"OHM-SLC-0042"}""");
    }

    [Fact]
    public void Given_inferred_polymorphism_in_the_options_when_written_then_the_leaf_round_trips()
    {
        var options = new JsonSerializerOptions { InferClosedTypePolymorphism = true };
        ChargerCommand command = new SetChargingProfile("OHM-SLC-0042", 1, 32m);

        var json = JsonSerializer.Serialize(command, options);

        json.ShouldStartWith("""{"$type":"SetChargingProfile","LimitKw":32,""");
        JsonSerializer.Deserialize<ChargerCommand>(json, options).ShouldBe(command);
    }

    [Fact]
    public void Given_inferred_polymorphism_on_the_type_when_written_then_the_type_name_is_used()
    {
        ConnectorEvent reserved = new ConnectorReserved("OHM-SLC-0042", 1, "04A1B2C3");

        var json = JsonSerializer.Serialize(reserved);

        json.ShouldStartWith("""{"$type":"ConnectorReserved","TokenUid":"04A1B2C3",""");
        JsonSerializer.Deserialize<ConnectorEvent>(json).ShouldBe(reserved);
    }

    [Fact]
    public void Given_explicit_discriminators_when_written_then_the_chosen_names_are_on_the_wire()
    {
        TariffEvent published = new TariffPublished("peak", 0.42m);

        var json = JsonSerializer.Serialize(published);

        json.ShouldBe(
            """{"$event":"tariffs.tariff-published.v1","PricePerKwh":0.42,"TariffId":"peak"}""");
        JsonSerializer.Deserialize<TariffEvent>(json).ShouldBe(published);
    }

    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
