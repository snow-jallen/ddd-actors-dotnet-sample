using System.Runtime.Serialization;
using System.Text.Json;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.ActorShell.Tests.Messaging;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.ActorShell.Tests.Tracing;

public sealed class TraceEnvelopeSerializerTests(ITestOutputHelper output)
    : TestKit(output: output)
{
    private const string TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private TraceEnvelopeSerializer Serializer => new((ExtendedActorSystem)Sys);

    [Fact]
    public void The_identifier_and_the_manifest_are_durable()
    {
        Serializer.Identifier.ShouldBe(9100);
        Serializer.Manifest(new TraceEnvelope(new Ping("hello"), TraceParent, null))
            .ShouldBe("ohmline.trace-envelope.v1");
    }

    [Fact]
    public void Given_an_envelope_when_it_round_trips_then_the_message_and_the_trace_come_back()
    {
        var envelope = new TraceEnvelope(new Ping("hello"), TraceParent, "ohmline=edge");

        var bytes = Serializer.ToBinary(envelope);

        Serializer.FromBinary(bytes, TraceEnvelopeSerializer.EnvelopeManifest)
            .ShouldBe(envelope);
    }

    [Fact]
    public void Given_an_envelope_when_written_then_the_message_keeps_its_own_serializer()
    {
        var ping = new Ping("hello");
        var inner = Sys.Serialization.FindSerializerFor(ping);

        var bytes = Serializer.ToBinary(new TraceEnvelope(ping, TraceParent, null));

        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        root.GetProperty("traceParent").GetString().ShouldBe(TraceParent);
        root.GetProperty("serializerId").GetInt32().ShouldBe(inner.Identifier);
        root.GetProperty("manifest").GetString()
            .ShouldBe(Akka.Serialization.Serialization.ManifestFor(inner, ping));
        root.GetProperty("payload").GetBytesFromBase64().ShouldBe(inner.ToBinary(ping));
    }

    [Fact]
    public void Given_anything_but_an_envelope_when_written_then_it_is_refused() =>
        Should.Throw<ArgumentException>(() => Serializer.ToBinary(new Ping("hello")));

    [Fact]
    public void Given_an_unknown_manifest_when_read_then_it_throws_a_serialization_exception() =>
        Should.Throw<SerializationException>(() =>
                Serializer.FromBinary("{}"u8.ToArray(), "ohmline.trace-envelope.v2"))
            .Message.ShouldContain("ohmline.trace-envelope.v2");
}
