using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Sessions;

namespace Ohmline.DeviceGateway.Ocpp.Translation;

/// <summary>
/// A command the gateway sends to Charging: to a charge point or to a session. The two
/// families share no base type, so a union makes them one closed set for the translator; what
/// is sent is the case inside, never the union (ADR 0003).
/// </summary>
public union ChargingCommand(ChargePointCommand, SessionCommand);
