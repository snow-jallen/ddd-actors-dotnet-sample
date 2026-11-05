using Akka.Actor;
using Ohmline.DeviceGateway.Connections;

namespace Ohmline.DeviceGateway.Tests;

/// <summary>The charger's WebSocket, as a test sees it: each frame sent goes to a probe.</summary>
internal sealed class RecordingLink(IActorRef charger) : IChargerLink
{
    public bool Closed { get; private set; }

    public void Send(string frame) => charger.Tell(new SentFrame(frame));

    public void Close() => Closed = true;
}

/// <summary>A frame the gateway sent its charger.</summary>
internal sealed record SentFrame(string Text) : INoSerializationVerificationNeeded;
