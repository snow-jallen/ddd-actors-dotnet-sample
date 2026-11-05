using System.Text;
using Akka.Actor;
using Akka.Serialization;
using Akka.TestKit.Xunit;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Testing;

namespace Ohmline.Charging.Actors.Tests.Serialization;

/// <summary>
/// A Charging command in a trace envelope (section 17.4), through Akka.NET's own serialization
/// as the cluster uses it: the envelope's serializer writes the trace, and Charging's writes the
/// command inside, under its own identifier and manifest.
/// </summary>
public sealed class TraceEnvelopeSerializationTests(ITestOutputHelper output)
    : TestKit(
        ActorTestConfig.With(Serializers.Charging, Envelope),
        nameof(TraceEnvelopeSerializationTests),
        output)
{
    private static readonly TraceEnvelope Sample = new(
        new GetChargePointStatus(ChargingSamples.ChargePoint),
        "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
        null);

    [Fact]
    public void Given_an_envelope_then_the_envelope_serializer_writes_it()
    {
        var serializer = Sys.Serialization.FindSerializerFor(Sample);

        serializer.ShouldBeOfType<TraceEnvelopeSerializer>().Identifier.ShouldBe(9100);
        Akka.Serialization.Serialization.ManifestFor(serializer, Sample)
            .ShouldBe("ohmline.trace-envelope.v1");
    }

    [Fact]
    public void Given_an_envelope_when_serialized_then_its_json_matches_the_golden_file()
    {
        var bytes = Sys.Serialization.FindSerializerFor(Sample).ToBinary(Sample);

        Golden.Verify(TraceEnvelopeSerializer.EnvelopeManifest, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Given_the_golden_json_when_read_then_it_gives_the_envelope_back()
    {
        var bytes = Encoding.UTF8.GetBytes(Golden.Read(TraceEnvelopeSerializer.EnvelopeManifest));

        Sys.Serialization.Deserialize(bytes, 9100, TraceEnvelopeSerializer.EnvelopeManifest)
            .ShouldBe(Sample);
    }

    private static SerializerDetails Envelope(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            TraceEnvelopeSerializer.Name,
            new TraceEnvelopeSerializer(system),
            [typeof(TraceEnvelope)]);
}
