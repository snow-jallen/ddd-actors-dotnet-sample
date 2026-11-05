using System.Collections.Immutable;
using Akka.Actor;
using Ohmline.Charging.Contracts;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.DeviceGateway.Ocpp.Translation;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DeviceGateway.Connections;

// What a connection actor hears from its own socket, timers and tasks. None of it ever leaves
// the gateway process, so none of it is serialized.

/// <summary>A text frame arrived on the WebSocket.</summary>
public sealed record FrameReceived(string Text) : INoSerializationVerificationNeeded;

/// <summary>The WebSocket closed.</summary>
public sealed record Disconnected : INoSerializationVerificationNeeded
{
    public static Disconnected Instance { get; } = new();
}

internal sealed record Silent : INoSerializationVerificationNeeded
{
    public static Silent Instance { get; } = new();
}

internal sealed record CallTimedOut(string MessageId) : INoSerializationVerificationNeeded;

internal sealed record TermsKnown(StartingTerms? Terms) : INoSerializationVerificationNeeded;

internal sealed record TermsUnknown(Exception Cause) : INoSerializationVerificationNeeded;

/// <summary>Every command of a call reached Charging; now the charger gets its answer.</summary>
internal sealed record Forwarded(OcppFrame Reply, ImmutableArray<CommandRejected> Rejected)
    : INoSerializationVerificationNeeded;

/// <summary>A command got no answer: the call is undone; the charger tries again.</summary>
internal sealed record ForwardFailed(string MessageId, ChargerContext Before, Exception Cause)
    : INoSerializationVerificationNeeded;

/// <summary>
/// A call Ohmline sent the charger, waiting for its answer: what it is for (the same request
/// asked again is this call), and everyone who asked for it.
/// </summary>
internal sealed record PendingCall(
    string Request,
    ImmutableArray<IActorRef> ReplyTo,
    ConnectorId ConnectorId,
    string? StartKey = null,
    long? ProfileVersion = null)
{
    public PendingCall AlsoFor(IActorRef asker) => this with { ReplyTo = ReplyTo.Add(asker) };
}
