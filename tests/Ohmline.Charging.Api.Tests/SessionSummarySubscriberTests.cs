using System.Threading.Channels;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Ohmline.Charging.Api.Live;
using Ohmline.Charging.Contracts.Live;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Tests;

/// <summary>
/// The per-request subscriber behind <c>GET /sessions/{id}/events</c>, with a probe in place of
/// the DistributedPubSub mediator.
/// </summary>
public sealed class SessionSummarySubscriberTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    private static readonly SessionId Mine =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001"));

    private static readonly SessionId Other =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000002"));

    private readonly Channel<SessionSummaryChanged> _changes =
        Channel.CreateUnbounded<SessionSummaryChanged>();

    private readonly TaskCompletionSource _subscribed = new();

    [Fact]
    public async Task Given_a_subscriber_then_it_passes_on_only_its_own_sessions_changes()
    {
        var mediator = CreateTestProbe();
        var subscriber = Sys.ActorOf(
            SessionSummarySubscriber.Props(mediator.Ref, Mine, _changes.Writer, _subscribed));

        var subscribe = await mediator.ExpectMsgAsync<Subscribe>(cancellationToken: Ct);
        mediator.Reply(new SubscribeAck(subscribe));
        await _subscribed.Task.WaitAsync(TimeSpan.FromSeconds(3), Ct);
        subscriber.Tell(new SessionSummaryChanged(Other, 2, "charging", 1.5m));
        subscriber.Tell(new SessionSummaryChanged(Mine, 3, "ended", 7.35m));

        subscribe.Topic.ShouldBe("session-summaries");
        subscribe.Ref.ShouldBe(subscriber);
        (await _changes.Reader.ReadAsync(Ct))
            .ShouldBe(new SessionSummaryChanged(Mine, 3, "ended", 7.35m));
    }

    [Fact]
    public async Task Given_the_request_ends_then_it_unsubscribes_and_closes_the_channel()
    {
        var mediator = CreateTestProbe();
        var subscriber = Sys.ActorOf(
            SessionSummarySubscriber.Props(mediator.Ref, Mine, _changes.Writer, _subscribed));
        await mediator.ExpectMsgAsync<Subscribe>(cancellationToken: Ct);

        Sys.Stop(subscriber);

        (await mediator.ExpectMsgAsync<Unsubscribe>(cancellationToken: Ct)).Ref
            .ShouldBe(subscriber);
        await _changes.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(3), Ct);
    }
}
