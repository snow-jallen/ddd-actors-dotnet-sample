using System.Text;
using Akka.Serialization;
using Akka.TestKit.Xunit;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Domain.ChargePoints;

namespace Ohmline.Charging.Actors.Tests;

/// <summary>
/// WRONG ON PURPOSE (branch wrong/ch10-s03-default-serializer). Without Charging's serializer,
/// Akka.NET 1.5 writes events with its default, Newtonsoft.Json. These tests show what the
/// journal would then store, and what a later rename does to it.
/// </summary>
public sealed class DefaultSerializerTests : TestKit
{
    private readonly ITestOutputHelper _output;

    public DefaultSerializerTests(ITestOutputHelper output)
        : base(output: output) => _output = output;

    private static readonly ConnectorReserved Reserved = new(
        ChargingSamples.ChargePoint, 1, ChargingSamples.Token, ChargingSamples.At.AddMinutes(15))
    {
        CommandId = ChargingSamples.Command,
        OccurredAt = ChargingSamples.At,
    };

    [Fact]
    public void Demonstrates_the_default_serializer_writes_the_type_and_assembly_into_the_event()
    {
        var serializer = Sys.Serialization.FindSerializerFor(Reserved);
        var bytes = serializer.ToBinary(Reserved);
        _output.WriteLine(Encoding.UTF8.GetString(bytes));

        serializer.ShouldBeOfType<NewtonSoftJsonSerializer>().Identifier.ShouldBe(1);
        Akka.Serialization.Serialization.ManifestFor(serializer, Reserved).ShouldBeEmpty();
        Encoding.UTF8.GetString(bytes).ShouldContain(
            "\"$type\":\"Ohmline.Charging.Domain.ChargePoints.ConnectorReserved, " +
            "Ohmline.Charging.Domain\"");
    }

    [Fact]
    public void Demonstrates_renaming_the_event_type_makes_stored_events_unreadable()
    {
        var stored = Sys.Serialization.FindSerializerFor(Reserved).ToBinary(Reserved);

        // A later version renames ConnectorReserved to ConnectorHeld. Replay reads the old bytes,
        // which still name the old type, exactly as the journal stored them.
        var replayedByTheRenamedCode = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(stored).Replace(".ConnectorReserved,", ".ConnectorHeld,"));

        var failure = Should.Throw<Exception>(() =>
            Sys.Serialization.Deserialize(replayedByTheRenamedCode, 1, string.Empty));
        _output.WriteLine($"{failure.Message} {failure.InnerException?.Message}");
        failure.Message.ShouldContain("ConnectorHeld");
    }
}
