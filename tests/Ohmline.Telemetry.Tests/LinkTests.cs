using System.Diagnostics;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Tracing;
using Ohmline.Testing;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// Parents versus links (section 17.7). A message that answers a request carries on the
/// request's trace; one that answers no one, such as a timer that fires minutes later, starts
/// a trace of its own with a link back to the span that started it. Both carry the
/// conversation's id, which ties the traces of one charging journey together.
/// </summary>
public sealed class LinkTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Envelopes.Serializer), nameof(LinkTests), output)
{
    private static readonly ActivitySource Source = new("Ohmline.Telemetry.Tests.Links");

    [Fact]
    public async Task Given_a_timer_started_in_a_trace_then_it_fires_in_a_new_trace_linked_to_it()
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var alarm = Sys.ActorOf(Props.Create(() => new Alarm(TestActor)), "alarm");
        await alarm.Ask<Armed>(new Arm(), RemainingOrDefault, Ct); // Recovered, untraced.
        await ExpectMsgAsync<Rang>(cancellationToken: Ct); // Its ring: no stale one is left.
        using var request = Source.StartActivity("POST /alarms")!;

        alarm.TellTraced(new Arm(), TestActor);

        await ExpectMsgAsync<Armed>(cancellationToken: Ct);
        var rang = await ExpectMsgAsync<Rang>(cancellationToken: Ct);
        var arm = await recorder.SingleAsync(request.TraceId, ActorShellDiagnostics.Name);
        var ring = await recorder.SingleAsync(
            ActivityContext.Parse(rang.ActivityId!, null).TraceId, ActorShellDiagnostics.Name);
        ring.Id.ShouldBe(rang.ActivityId);
        ring.TraceId.ShouldNotBe(request.TraceId);
        ring.ParentSpanId.ShouldBe(default);
        ring.Links.ShouldHaveSingleItem().Context.SpanId.ShouldBe(arm.SpanId);
        ring.Links.Single().Context.TraceId.ShouldBe(request.TraceId);
        arm.GetTagItem("messaging.message.conversation_id").ShouldBe("alarm-1");
        ring.GetTagItem("messaging.message.conversation_id").ShouldBe("alarm-1");
    }

    [Fact]
    public async Task Given_a_traced_timer_message_then_a_plain_actor_processes_it_in_a_span()
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var bell = Sys.ActorOf(Props.Create(() => new Bell(TestActor)), "bell");
        using var request = Source.StartActivity("POST /bells")!;

        bell.TellTraced(new Arm(), TestActor);

        var rang = await ExpectMsgAsync<Rang>(cancellationToken: Ct);
        var spans = await recorder.WaitForAsync(request.TraceId, "process Bell", count: 2);
        spans.Where(span => span.DisplayName == "process Bell").Select(span => span.Id)
            .ShouldContain(rang.ActivityId);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Arm;

    private sealed record Armed;

    private sealed record Ring;

    private sealed record Rang(string? ActivityId);

    /// <summary>The same, as a plain actor: the timer carries on the trace that armed it.</summary>
    private sealed class Bell : TracedReceiveActor, IWithTimers
    {
        public Bell(IActorRef listener)
        {
            Receive<Arm>(_ => Timers.StartSingleTimer(
                "ring", TraceEnvelope.Wrap(new Ring()), TimeSpan.FromMilliseconds(50)));
            Receive<Ring>(_ => listener.Tell(new Rang(Activity.Current?.Id)));
        }

        public ITimerScheduler Timers { get; set; } = null!;
    }

    /// <summary>Armed by a request, it rings a moment later, on a timer.</summary>
    private sealed class Alarm : TracedPersistentActor, IWithTimers
    {
        public Alarm(IActorRef listener)
        {
            Command<Arm>(_ =>
            {
                Timers.StartSingleTimer(
                    "ring", TraceEnvelope.Wrap(new Ring()), TimeSpan.FromMilliseconds(50));
                Sender.Tell(new Armed());
            });
            Command<Ring>(_ => listener.Tell(new Rang(Activity.Current?.Id)));
        }

        public override string PersistenceId => "alarm-1";

        public ITimerScheduler Timers { get; set; } = null!;

        protected override string ConversationId => PersistenceId;

        protected override bool ContinuesTrace(object message) => message is not Ring;
    }
}
