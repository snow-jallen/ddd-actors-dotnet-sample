using System.Text.Json;
using Ohmline.ActorShell.Serialization;

namespace Ohmline.ActorShell.Tests.Serialization;

public sealed class ClosedHierarchiesTests
{
    [Fact]
    public void Given_a_case_inside_a_message_when_written_then_it_carries_its_kind()
    {
        var options = new JsonSerializerOptions();
        options.AddClosedHierarchy<Fault>(
            (typeof(GroundFailure), "ground-failure"),
            (typeof(OverTemperature), "over-temperature"));
        var report = new FaultReport("OHM-SLC-0042", new OverTemperature(81.5m));

        var json = JsonSerializer.Serialize(report, options);

        json.ShouldBe(
            """{"ChargePoint":"OHM-SLC-0042","Fault":"""
            + """{"$kind":"over-temperature","Celsius":81.5}}""");
        JsonSerializer.Deserialize<FaultReport>(json, options).ShouldBe(report);
    }

    [Fact]
    public void Given_a_hierarchy_with_a_case_left_out_when_added_then_it_is_refused()
    {
        var options = new JsonSerializerOptions();

        Should.Throw<ArgumentException>(() =>
                options.AddClosedHierarchy<Fault>((typeof(GroundFailure), "ground-failure")))
            .Message.ShouldContain("OverTemperature is a Fault without a kind.");
    }
}
