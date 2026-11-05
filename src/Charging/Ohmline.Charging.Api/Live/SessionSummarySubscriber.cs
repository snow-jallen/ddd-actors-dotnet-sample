using System.Threading.Channels;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Ohmline.Charging.Contracts.Live;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Live;

/// <summary>
/// One request's subscription to one session's summaries (section 14.4): a short-lived actor,
/// started for a request and stopped when it ends. It subscribes to the
/// <c>session-summaries</c> topic on this node's DistributedPubSub mediator, and writes its
/// session's changes to the request's channel; the topic carries every session's.
/// </summary>
public sealed class SessionSummarySubscriber : ReceiveActor
{
    private readonly IActorRef _mediator;
    private readonly ChannelWriter<SessionSummaryChanged> _changes;

    public SessionSummarySubscriber(
        IActorRef mediator,
        SessionId sessionId,
        ChannelWriter<SessionSummaryChanged> changes,
        TaskCompletionSource subscribed)
    {
        _mediator = mediator;
        _changes = changes;

        Receive<SubscribeAck>(_ => subscribed.TrySetResult());
        Receive<SessionSummaryChanged>(
            changed => changed.SessionId == sessionId, changed => _changes.TryWrite(changed));
        Receive<SessionSummaryChanged>(_ => { }); // Another session's.
    }

    public static Props Props(
        IActorRef mediator,
        SessionId sessionId,
        ChannelWriter<SessionSummaryChanged> changes,
        TaskCompletionSource subscribed) =>
        Akka.Actor.Props.Create(
            () => new SessionSummarySubscriber(mediator, sessionId, changes, subscribed));

    protected override void PreStart() =>
        _mediator.Tell(new Subscribe(LiveTopics.SessionSummaries, Self));

    protected override void PostStop()
    {
        _mediator.Tell(new Unsubscribe(LiveTopics.SessionSummaries, Self));
        _changes.TryComplete();
    }
}
