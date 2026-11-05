using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Contracts.Chargers;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Node.Tests;

/// <summary>
/// The journey's charger port on the node's real DistributedPubSub mediator (section 16.7):
/// a command published on the charge point's topic reaches whoever holds the charger, and the
/// answer comes straight back to the port.
/// </summary>
public sealed class GatewayChargerCommandPortTests(ITestOutputHelper output) : ShardedNode(output)
{
    private static readonly ChargePointId Charger = new("OHM-SLC-0042");

    private static readonly ConnectorId Connector = new(Charger, 1);

    [Fact]
    public async Task Given_a_connected_charger_then_a_remote_start_reaches_it_and_is_answered()
    {
        var heard = CreateTestProbe();
        await ConnectAsync(heard, new ChargerCommandAccepted(Connector));
        var key = IdempotencyKey.Of(new JourneyId(Guid.CreateVersion7()), IdempotencyKey.Charger);
        var session = new SessionId(key.JourneyId.Value);

        var result = await new GatewayChargerCommandPort(Sys).RemoteStartAsync(
            key, Connector, new TokenUid("04A2B3C4D5E6F7"), session, new Power(22m), Ct);

        result.ShouldBe(new ChargerAccepted(Connector));
        (await heard.ExpectMsgAsync<RemoteStart>(cancellationToken: Ct)).IdempotencyKey
            .ShouldBe(key.Value);
    }

    [Fact]
    public async Task Given_a_charger_that_refuses_then_the_journey_hears_why()
    {
        await ConnectAsync(
            CreateTestProbe(), new ChargerCommandRejected(Connector, "The charger refused."));

        var result = await new GatewayChargerCommandPort(Sys).SetChargingProfileAsync(
            Connector, new Power(11m), version: 4, Ct);

        result.ShouldBe(new ChargerRejected(Connector, "The charger refused."));
    }

    /// <summary>
    /// A stand-in for the gateway's connection actor, subscribed to the charge point's topic,
    /// that answers every command the same way.
    /// </summary>
    private async Task ConnectAsync(Akka.TestKit.TestProbe heard, ChargerCommandReply answer)
    {
        var charger = Sys.ActorOf(Props.Create(() => new StandIn(heard.Ref, answer)));
        await DistributedPubSub.Get(Sys).Mediator.Ask<SubscribeAck>(
            new Subscribe(ChargerTopics.Of(Charger), charger), Patience, Ct);
    }

    private sealed class StandIn : ReceiveActor
    {
        public StandIn(IActorRef heard, ChargerCommandReply answer) =>
            Receive<ChargerCommand>(command =>
            {
                heard.Forward(command);
                Sender.Tell(answer);
            });
    }
}
