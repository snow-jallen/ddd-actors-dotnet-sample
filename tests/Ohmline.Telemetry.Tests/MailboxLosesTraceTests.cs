using System.Diagnostics;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.Testing;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// Why traces stop at the mailbox (section 17.3). <see cref="Activity.Current"/> is an
/// AsyncLocal: it flows with the execution context, through awaits and Task.Run. A Tell puts
/// the message in a mailbox, and a dispatcher thread runs the actor later, for many senders at
/// once, without anyone's execution context. So the actor sees no current activity at all.
/// </summary>
public sealed class MailboxLosesTraceTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.Config, nameof(MailboxLosesTraceTests), output)
{
    private static readonly ActivitySource Source = new("Ohmline.Telemetry.Tests.Mailbox");

    [Fact]
    public async Task Given_a_span_when_a_message_is_told_then_the_actor_sees_no_current_activity()
    {
        using var recorder = new SpanRecorder(Source.Name);
        var witness = Sys.ActorOf(Witness.Props, "witness");

        using (var request = Source.StartActivity("POST /journeys"))
        {
            request.ShouldNotBeNull();
            witness.Tell(new WhatIsCurrent(), TestActor);
        }

        var seen = await ExpectMsgAsync<Seen>(cancellationToken: Ct);
        seen.ActivityId.ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_span_when_an_actor_is_asked_then_it_sees_no_current_activity_either()
    {
        using var recorder = new SpanRecorder(Source.Name);
        var witness = Sys.ActorOf(Witness.Props, "witness");
        using var request = Source.StartActivity("GET /journeys/{id}");

        var seen = await witness.Ask<Seen>(new WhatIsCurrent(), RemainingOrDefault, Ct);

        seen.ActivityId.ShouldBeNull();
        Activity.Current.ShouldBeSameAs(request); // The asker still has its span after the await.
    }

    /// <summary>The contrast: the same span does flow where the execution context does.</summary>
    [Fact]
    public async Task Given_a_span_when_work_is_queued_with_Task_Run_then_it_sees_the_span()
    {
        using var recorder = new SpanRecorder(Source.Name);
        using var request = Source.StartActivity("POST /journeys");

        var seen = await Task.Run(() => Activity.Current?.Id, Ct);

        seen.ShouldBe(request!.Id);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record WhatIsCurrent;

    private sealed record Seen(string? ActivityId);

    /// <summary>Answers with the id of whatever activity is current while it runs.</summary>
    private sealed class Witness : ReceiveActor
    {
        public Witness() =>
            Receive<WhatIsCurrent>(_ => Sender.Tell(new Seen(Activity.Current?.Id)));

        public static Props Props { get; } = Akka.Actor.Props.Create(() => new Witness());
    }
}
