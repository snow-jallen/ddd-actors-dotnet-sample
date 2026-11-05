using Microsoft.Extensions.Logging;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// A charge point that agrees to everything and does nothing but log it. Retired from the node
/// when the Device Gateway connected real ones (section 16.7); tests that run a journey without
/// a gateway put it behind the port, and report the charger's side through the regions.
/// </summary>
public sealed class LoggingChargerCommandPort(ILogger<LoggingChargerCommandPort> log)
    : IChargerCommandPort
{
    public Task<ChargerCommandResult> RemoteStartAsync(
        IdempotencyKey key,
        ConnectorId connectorId,
        TokenUid tokenUid,
        SessionId sessionId,
        Power limit,
        CancellationToken cancellationToken)
    {
        log.LogInformation(
            "RemoteStart {Key} on {Connector} for {Token}, session {Session}, at most {Limit}",
            key,
            connectorId,
            tokenUid,
            sessionId,
            limit);
        return Accepted(connectorId);
    }

    public Task<ChargerCommandResult> RemoteStopAsync(
        ConnectorId connectorId, SessionId sessionId, CancellationToken cancellationToken)
    {
        log.LogInformation("RemoteStop on {Connector}, session {Session}", connectorId, sessionId);
        return Accepted(connectorId);
    }

    public Task<ChargerCommandResult> SetChargingProfileAsync(
        ConnectorId connectorId, Power limit, long version, CancellationToken cancellationToken)
    {
        log.LogInformation(
            "SetChargingProfile on {Connector}: at most {Limit} (allocation {Version})",
            connectorId,
            limit,
            version);
        return Accepted(connectorId);
    }

    private static Task<ChargerCommandResult> Accepted(ConnectorId connectorId) =>
        Task.FromResult<ChargerCommandResult>(new ChargerAccepted(connectorId));
}
