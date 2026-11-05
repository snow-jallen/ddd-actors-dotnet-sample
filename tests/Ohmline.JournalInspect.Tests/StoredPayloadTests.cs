using Akka.TestKit.Xunit;
using Ohmline.ActorShell.Tracing;
using Ohmline.Testing;

namespace Ohmline.JournalInspect.Tests;

public sealed class StoredPayloadTests(ITestOutputHelper output)
    : TestKit(
        ActorTestConfig.With(Payloads.Charging, Payloads.Envelopes),
        nameof(StoredPayloadTests),
        output)
{
    [Fact]
    public void Given_an_event_its_serializer_wrote_then_its_json_is_shown_as_it_is()
    {
        var (serializerId, manifest, bytes) = Payloads.Write(Sys, Payloads.Reservation);

        var payload = StoredPayload.Read(serializerId, manifest, bytes);

        payload.ShouldBe(new StoredPayload(
            9101, "charging.connector-reserved.v1", Payloads.Json(bytes)));
        payload.Text.ShouldContain("\"tokenUid\":\"04A2B3C4D5E6F7\"");
    }

    [Fact]
    public void Given_an_event_in_a_trace_envelope_then_the_event_inside_is_shown()
    {
        var (_, _, inner) = Payloads.Write(Sys, Payloads.Reservation);
        var (serializerId, manifest, bytes) = Payloads.Write(
            Sys, new TraceEnvelope(Payloads.Reservation, Payloads.TraceParent, null));

        var payload = StoredPayload.Read(serializerId, manifest, bytes);

        payload.SerializerId.ShouldBe(9101);
        payload.Manifest.ShouldBe("charging.connector-reserved.v1");
        payload.Text.ShouldBe(Payloads.Json(inner));
        payload.TraceParent.ShouldBe(Payloads.TraceParent);
        payload.Envelope.ShouldNotBeNull().SerializerId.ShouldBe(9100);
        payload.Envelope.Manifest.ShouldBe("ohmline.trace-envelope.v1");
    }

    [Fact]
    public void Given_bytes_that_are_not_json_then_they_are_shown_as_base64()
    {
        var payload = StoredPayload.Read(4, "", [0xCA, 0xFE]);

        payload.Text.ShouldBe("base64:yv4=");
    }

    [Fact]
    public void The_tool_knows_the_envelope_by_the_durable_names_the_serializer_writes()
    {
        StoredPayload.TraceEnvelopeSerializerId.ShouldBe(TraceEnvelopeSerializer.Id);
        StoredPayload.TraceEnvelopeManifest.ShouldBe(TraceEnvelopeSerializer.EnvelopeManifest);
    }
}
