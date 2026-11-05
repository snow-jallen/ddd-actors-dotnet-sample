using Akka.Actor;
using Akka.Configuration;
using Ohmline.ActorShell.Telemetry;
using Ohmline.Testing;
using Ohmline.Testing.Metrics;

namespace Ohmline.ActorShell.Tests.Telemetry;

/// <summary>
/// The shell's listeners on the event stream (section 18.2): dead letters, and the lifecycle
/// events Akka.NET publishes once actor telemetry is on.
/// </summary>
public sealed class ShellListenerTests : Akka.TestKit.Xunit.TestKit
{
    private static readonly Config Telemetry = ConfigurationFactory
        .ParseString("akka.actor.telemetry.enabled = on")
        .WithFallback(ActorTestConfig.Config);

    private readonly TestMeterFactory _meters = new();
    private readonly InstrumentRecorder _recorded;
    private readonly ActorShellMetrics _metrics;

    public ShellListenerTests(ITestOutputHelper output)
        : base(Telemetry, nameof(ShellListenerTests), output)
    {
        _recorded = new InstrumentRecorder(_meters);
        _metrics = new ActorShellMetrics(_meters);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_message_to_an_actor_that_is_gone_then_it_counts_by_its_type()
    {
        await StartedAsync(Props.Create(() => new DeadLetterMetricsListener(_metrics)));
        var gone = Sys.ActorOf(Props.Create<Echo>());
        await WatchAsync(gone);
        Sys.Stop(gone);
        await ExpectTerminatedAsync(gone, cancellationToken: Ct);

        gone.Tell(new Ping());

        await AwaitAssertAsync(
            () => _recorded.Of(ActorShellMetrics.DeadLetters)
                .ShouldContain(letter => letter.Tags[ActorShellMetrics.MessageTypeTag] == "Ping"),
            cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_an_actor_that_starts_fails_and_stops_then_each_is_counted_by_type()
    {
        await StartedAsync(Props.Create(() => new LifecycleMetricsListener(_metrics)));
        var echo = Sys.ActorOf(Props.Create<Echo>());
        await WatchAsync(echo);

        await EventFilter.Exception<InvalidOperationException>().ExpectOneAsync(
            () =>
            {
                echo.Tell(new Fail());
                return Task.CompletedTask;
            },
            Ct);
        Sys.Stop(echo);
        await ExpectTerminatedAsync(echo, cancellationToken: Ct);

        await AwaitAssertAsync(
            () => Lifecycle(nameof(Echo)).ShouldBe(["started", "restarted", "stopped"]),
            cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_Akka_NETs_own_actors_then_only_Ohmlines_are_counted()
    {
        await StartedAsync(Props.Create(() => new LifecycleMetricsListener(_metrics)));
        var probe = CreateTestProbe(); // The test kit's own actor, in Akka.TestKit.
        await probe.Ref.Ask<ActorIdentity>(new Identify(null), RemainingOrDefault, Ct);

        await StartedAsync(Props.Create<Echo>());

        await AwaitAssertAsync(
            () => Lifecycle(nameof(Echo)).ShouldBe(["started"]), cancellationToken: Ct);
        _recorded.Of(ActorShellMetrics.ActorLifecycle)
            .Select(happened => happened.Tags[ActorShellMetrics.ActorTypeTag])
            .ShouldBe([nameof(LifecycleMetricsListener), nameof(Echo)], ignoreOrder: true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recorded.Dispose();
            _meters.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Started, and through its PreStart: a listener has subscribed by then.</summary>
    private async Task<IActorRef> StartedAsync(Props props)
    {
        var actor = Sys.ActorOf(props);
        await actor.Ask<ActorIdentity>(new Identify(null), RemainingOrDefault, Ct);
        return actor;
    }

    private string?[] Lifecycle(string actorType) =>
    [
        .. _recorded.Of(ActorShellMetrics.ActorLifecycle)
            .Where(happened => happened.Tags[ActorShellMetrics.ActorTypeTag] == actorType)
            .Select(happened => happened.Tags[ActorShellMetrics.LifecycleTag]),
    ];

    public sealed record Ping;

    public sealed record Fail;

    private sealed class Echo : ReceiveActor
    {
        public Echo()
        {
            Receive<Ping>(ping => Sender.Tell(ping));
            Receive<Fail>(_ => throw new InvalidOperationException("Told to fail."));
        }
    }
}
