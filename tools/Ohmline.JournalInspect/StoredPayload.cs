using System.Text;
using System.Text.Json;

namespace Ohmline.JournalInspect;

/// <summary>
/// A payload as a person can read it. Ohmline's serializers write JSON, so most payloads are
/// shown as they are. A trace envelope (section 17.7) holds another serializer's id, manifest
/// and bytes, so it is opened, and the message inside is shown with its own id and manifest.
/// Anything that is not JSON is shown as base64.
/// </summary>
public sealed record StoredPayload(
    int? SerializerId, string? Manifest, string Text, string? TraceParent = null)
{
    /// <summary>Durable, as in ADR 0007: the trace envelope's serializer id and manifest.</summary>
    public const int TraceEnvelopeSerializerId = 9100;

    /// <inheritdoc cref="TraceEnvelopeSerializerId"/>
    public const string TraceEnvelopeManifest = "ohmline.trace-envelope.v1";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>The envelope around the message, if it came in one.</summary>
    public StoredPayload? Envelope { get; init; }

    public static StoredPayload Read(int? serializerId, string? manifest, byte[] bytes)
    {
        if (serializerId == TraceEnvelopeSerializerId && manifest == TraceEnvelopeManifest)
        {
            var envelope = JsonSerializer.Deserialize<EnvelopeShape>(bytes, Web)
                ?? throw new JsonException("The trace envelope is null.");
            var outer = new StoredPayload(serializerId, manifest, "", envelope.TraceParent);
            return Read(envelope.SerializerId, envelope.Manifest, envelope.Payload) with
            {
                TraceParent = envelope.TraceParent,
                Envelope = outer,
            };
        }

        return new StoredPayload(serializerId, manifest, AsText(bytes));
    }

    private static string AsText(byte[] bytes)
    {
        try
        {
            using var json = JsonDocument.Parse(bytes);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (JsonException)
        {
            return $"base64:{Convert.ToBase64String(bytes)}";
        }
    }

    /// <summary>The envelope as <c>TraceEnvelopeSerializer</c> writes it.</summary>
    private sealed record EnvelopeShape(
        string TraceParent, string? TraceState, int SerializerId, string Manifest, byte[] Payload);
}
