using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Contracts.Chargers;

/// <summary>
/// What Charging asks a connected charge point to do, through the Device Gateway (section
/// 16.7): published on the charge point's topic (<see cref="ChargerTopics"/>), where the
/// gateway's connection to that charger listens, and answered with a
/// <see cref="ChargerCommandReply"/>. The gateway turns each into an OCPP call.
/// </summary>
public closed record ChargerCommand(ConnectorId ConnectorId);

/// <summary>
/// Start a session for the token on the connector, drawing at most the limit. The key is the
/// journey's: the same start asked again, after a lost answer, is answered as the first was.
/// </summary>
public sealed record RemoteStart(
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    SessionId SessionId,
    Power Limit,
    string IdempotencyKey)
    : ChargerCommand(ConnectorId);

/// <summary>Stop the session on the connector.</summary>
public sealed record RemoteStop(ConnectorId ConnectorId, SessionId SessionId)
    : ChargerCommand(ConnectorId);

/// <summary>
/// Draw at most this much on the connector from now on (OCPP's name). The version is the site
/// allocation's (section 16.8): a charger is never sent back to an older allocation.
/// </summary>
public sealed record SetChargingProfile(ConnectorId ConnectorId, Power Limit, long Version)
    : ChargerCommand(ConnectorId);
