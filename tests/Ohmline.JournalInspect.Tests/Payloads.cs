using System.Text;
using Akka.Actor;
using Akka.Serialization;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.JournalInspect.Tests;

/// <summary>
/// Real payloads, written by the serializers the node uses: a reservation at the bus depot on
/// 2026-11-04 at 07:40, alone and in a trace envelope.
/// </summary>
internal static class Payloads
{
    public const string TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    public static readonly DateTimeOffset At = new(2026, 11, 4, 7, 40, 0, TimeSpan.FromHours(-7));

    public static ConnectorReserved Reservation { get; } =
        new ConnectorReserved(
            new ChargePointId("OHM-SLC-0042"), 1, new TokenUid("04A2B3C4D5E6F7"), At.AddMinutes(15))
        {
            CommandId = new CommandId(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042")),
            OccurredAt = At,
        };

    public static SerializerDetails Charging(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            ChargingSerializer.Name,
            new ChargingSerializer(system),
            [.. ChargingSerializer.BoundTypes]);

    public static SerializerDetails Envelopes(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            TraceEnvelopeSerializer.Name,
            new TraceEnvelopeSerializer(system),
            [typeof(TraceEnvelope)]);

    public static (int SerializerId, string Manifest, byte[] Bytes) Write(
        ActorSystem system, object message)
    {
        var serializer = system.Serialization.FindSerializerFor(message);
        return (
            serializer.Identifier,
            Serialization.ManifestFor(serializer, message),
            serializer.ToBinary(message));
    }

    public static string Json(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
