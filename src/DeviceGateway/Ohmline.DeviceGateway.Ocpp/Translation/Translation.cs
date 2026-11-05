using System.Collections.Immutable;
using Ohmline.DeviceGateway.Ocpp.Frames;

namespace Ohmline.DeviceGateway.Ocpp.Translation;

/// <summary>
/// What a charger's call comes to (section 16.6): commands for Charging and an answer for the
/// charger; nothing for Charging, with an answer all the same; or a call that makes no sense,
/// answered with an error. A charger always gets an answer.
/// </summary>
public union Translation(Translated, Ignored, Malformed);

/// <summary>
/// Understood: the commands to send to Charging, in order (possibly none, for a heartbeat),
/// the answer for the charger once they are sent, and what the gateway now remembers.
/// </summary>
public sealed record Translated(
    ImmutableArray<ChargingCommand> Commands, OcppFrame Reply, ChargerContext Context);

/// <summary>Understood, and nothing Ohmline acts on: why, and the answer for the charger.</summary>
public sealed record Ignored(string Reason, OcppFrame Reply);

/// <summary>Not a call the gateway can read: why, and the error for the charger.</summary>
public sealed record Malformed(string Reason, CallError Reply);
