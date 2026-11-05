using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Microsoft.AspNetCore.SignalR;
using Ohmline.Charging.Contracts.Live;

namespace Ohmline.Charging.Api.Live;

/// <summary>
/// Passes the projections' live updates on to the SignalR clients connected to this node
/// (section 14.5). One per node, subscribed to the same DistributedPubSub topics as the
/// Server-Sent Events: DistributedPubSub is the backplane, so every node hears every update and
/// sends it to its own connections, and no Redis is needed.
/// </summary>
/// <remarks>
/// It sends one update at a time, so a session's versions reach its group in the order they
/// were published. A send that fails restarts the bridge, which subscribes again; the update is
/// lost, and clients catch up by reading, as after any gap.
/// </remarks>
public sealed class LiveUpdatesBridge : ReceiveActor
{
    /// <summary>The bridge's name under <c>/user</c>.</summary>
    public const string Name = "live-updates";

    public LiveUpdatesBridge(IHubContext<ChargingHub> hub)
    {
        ReceiveAsync<SessionSummaryChanged>(changed => hub.Clients
            .Group(ChargingHub.SessionGroup(changed.SessionId))
            .SendAsync(ChargingHub.SessionSummaryChanged, SessionSummaryDto.From(changed)));
        Receive<SubscribeAck>(_ => { });
    }

    protected override void PreStart() =>
        DistributedPubSub.Get(Context.System).Mediator
            .Tell(new Subscribe(LiveTopics.SessionSummaries, Self));
}
