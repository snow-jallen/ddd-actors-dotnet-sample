using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Contracts.Chargers;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// The journey's charger port, behind which the Device Gateway reaches the charge points
/// (section 16.7): each command is published on the charge point's topic, where the gateway
/// node that holds the charger's WebSocket listens, and its answer comes straight back. Charging
/// never knows which gateway node a charger is connected to.
/// </summary>
/// <remarks>
/// <para>
/// DistributedPubSub's registry is gossiped: for about a second after a charger connects, other
/// nodes may not know its topic has a subscriber, and a command published there goes nowhere.
/// So the port asks again every <see cref="Attempt"/>, under the same request; the gateway
/// sends the charger one call however often it is asked, and answers every ask.
/// </para>
/// <para>
/// A charger that is not connected has no subscriber at all: after <see cref="Attempts"/>
/// asks the task fails, and the journey's own timers decide what that means (section 15.5).
/// </para>
/// </remarks>
public sealed class GatewayChargerCommandPort(ActorSystem system) : IChargerCommandPort
{
    public static readonly TimeSpan Attempt = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Seven asks, 35 s in all: longer than the gateway waits for its charger (30 s), so that
    /// a charger that does not answer comes back as a refusal rather than as silence.
    /// </summary>
    public const int Attempts = 7;

    public Task<ChargerCommandResult> RemoteStartAsync(
        IdempotencyKey key,
        ConnectorId connectorId,
        TokenUid tokenUid,
        SessionId sessionId,
        Power limit,
        CancellationToken cancellationToken) =>
        AskAsync(
            new RemoteStart(connectorId, tokenUid, sessionId, limit, key.Value),
            cancellationToken);

    public Task<ChargerCommandResult> RemoteStopAsync(
        ConnectorId connectorId, SessionId sessionId, CancellationToken cancellationToken) =>
        AskAsync(new RemoteStop(connectorId, sessionId), cancellationToken);

    public Task<ChargerCommandResult> SetChargingProfileAsync(
        ConnectorId connectorId, Power limit, long version, CancellationToken cancellationToken) =>
        AskAsync(new SetChargingProfile(connectorId, limit, version), cancellationToken);

    /// <summary>
    /// The mediator hands the published command to the subscriber with the asker as its sender,
    /// so the gateway's answer goes straight back to this ask. The command travels in a trace
    /// envelope, taken from the journey's span before the first await (section 17.4).
    /// </summary>
    private async Task<ChargerCommandResult> AskAsync(
        ChargerCommand command, CancellationToken cancellationToken)
    {
        var mediator = DistributedPubSub.Get(system).Mediator;
        var publish = new Publish(
            ChargerTopics.Of(command.ConnectorId.ChargePointId), TraceEnvelope.Wrap(command));
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var reply = await mediator.Ask<ChargerCommandReply>(
                    publish, Attempt, cancellationToken);
                return reply switch
                {
                    ChargerCommandAccepted accepted => new ChargerAccepted(accepted.ConnectorId),
                    ChargerCommandRejected rejected =>
                        new ChargerRejected(rejected.ConnectorId, rejected.Reason),
                };
            }
            catch (AskTimeoutException) when (attempt < Attempts)
            {
                // Not yet known on this node, or the charger is slow: ask again.
            }
        }
    }
}
