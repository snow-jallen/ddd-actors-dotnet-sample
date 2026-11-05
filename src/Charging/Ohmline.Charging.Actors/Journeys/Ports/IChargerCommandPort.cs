using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// How Charging tells a charge point what to do: a port, because the charge points are reached
/// through the Device Gateway (Chapter 16), which speaks OCPP-style frames, not Charging's
/// messages. The answer says whether the charge point will do it.
/// </summary>
public interface IChargerCommandPort
{
    /// <summary>Start a session for the token on the connector, drawing at most this.</summary>
    Task<ChargerCommandResult> RemoteStartAsync(
        ConnectorId connectorId,
        TokenUid tokenUid,
        SessionId sessionId,
        Power limit,
        CancellationToken cancellationToken);

    /// <summary>Stop the session on the connector.</summary>
    Task<ChargerCommandResult> RemoteStopAsync(
        ConnectorId connectorId, SessionId sessionId, CancellationToken cancellationToken);

    /// <summary>Draw at most this much on the connector from now on (OCPP's name).</summary>
    Task<ChargerCommandResult> SetChargingProfileAsync(
        ConnectorId connectorId, Power limit, CancellationToken cancellationToken);
}
