using Microsoft.Extensions.Logging;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// A charge point that agrees to everything and does nothing but log it, until the Device
/// Gateway connects real ones (section 16.7). The session itself is started, as before, by the
/// charger's report: in Development, the pretend charger's endpoints and <c>POST /sessions</c>.
/// </summary>
public sealed class LoggingChargerCommandPort(ILogger<LoggingChargerCommandPort> log)
    : IChargerCommandPort
{
    public Task<ChargerCommandResult> RemoteStartAsync(
        ConnectorId connectorId,
        TokenUid tokenUid,
        SessionId sessionId,
        Power limit,
        CancellationToken cancellationToken)
    {
        log.LogInformation(
            "RemoteStart on {Connector} for {Token}, session {Session}, at most {Limit}",
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
        ConnectorId connectorId, Power limit, CancellationToken cancellationToken)
    {
        log.LogInformation(
            "SetChargingProfile on {Connector}: at most {Limit}", connectorId, limit);
        return Accepted(connectorId);
    }

    private static Task<ChargerCommandResult> Accepted(ConnectorId connectorId) =>
        Task.FromResult<ChargerCommandResult>(new ChargerAccepted(connectorId));
}
