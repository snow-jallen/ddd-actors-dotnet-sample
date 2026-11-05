using System.Runtime.Serialization;
using System.Text.Json;
using Akka.Actor;
using Akka.Serialization;

namespace Ohmline.ActorShell.Tracing;

/// <summary>
/// Writes a <see cref="TraceEnvelope"/> for the wire and, from section 17.7, the journal. The
/// message inside is written by whichever serializer Akka.NET has for it, and the envelope keeps
/// that serializer's identifier, its manifest and its bytes beside the trace: so the envelope
/// holds any message, and reading it back needs nothing but the inner serializer.
/// </summary>
/// <remarks>Durable: its identifier and manifest are ADR 0007's, for good.</remarks>
public sealed class TraceEnvelopeSerializer(ExtendedActorSystem system)
    : SerializerWithStringManifest(system)
{
    /// <summary>The serializer's name in the actor system's configuration.</summary>
    public const string Name = "trace-envelope";

    /// <summary>Durable: Ohmline's first serializer id (9100–9199 are Ohmline's).</summary>
    public const int Id = 9100;

    /// <summary>Durable: the one manifest it writes.</summary>
    public const string EnvelopeManifest = "ohmline.trace-envelope.v1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override int Identifier => Id;

    public override string Manifest(object o) =>
        o is TraceEnvelope
            ? EnvelopeManifest
            : throw new ArgumentException($"{Name} writes trace envelopes, not {o.GetType()}.");

    public override byte[] ToBinary(object obj)
    {
        _ = Manifest(obj);
        var envelope = (TraceEnvelope)obj;
        var inner = system.Serialization.FindSerializerFor(envelope.Message);
        var wire = new Wire(
            envelope.TraceParent,
            envelope.TraceState,
            inner.Identifier,
            Akka.Serialization.Serialization.ManifestFor(inner, envelope.Message),
            inner.ToBinary(envelope.Message));
        return JsonSerializer.SerializeToUtf8Bytes(wire, Json);
    }

    public override object FromBinary(byte[] bytes, string manifest)
    {
        if (manifest != EnvelopeManifest)
        {
            throw new SerializationException($"{Name} does not know the manifest '{manifest}'.");
        }

        var wire = JsonSerializer.Deserialize<Wire>(bytes, Json)
            ?? throw new SerializationException($"The '{manifest}' payload is null.");
        var message = system.Serialization.Deserialize(
            wire.Payload, wire.SerializerId, wire.Manifest);
        return new TraceEnvelope(message, wire.TraceParent, wire.TraceState);
    }

    /// <summary>
    /// The envelope as written: the trace, and the message as its own serializer wrote it.
    /// </summary>
    private sealed record Wire(
        string TraceParent, string? TraceState, int SerializerId, string Manifest, byte[] Payload);
}
