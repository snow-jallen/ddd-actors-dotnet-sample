using Akka.Actor;
using Akka.Serialization;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.Telemetry.Tests;

/// <summary>The envelope's serializer, registered for a test as every node registers it.</summary>
internal static class Envelopes
{
    public static SerializerDetails Serializer(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            TraceEnvelopeSerializer.Name,
            new TraceEnvelopeSerializer(system),
            [typeof(TraceEnvelope)]);
}
