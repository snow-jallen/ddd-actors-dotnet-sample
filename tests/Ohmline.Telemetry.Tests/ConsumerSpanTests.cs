using System.Diagnostics;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Tracing;
using Ohmline.Testing;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// The shell carries on the trace (section 17.5): a traced message is processed in a CONSUMER
/// span, <c>process {ActorType}</c>, the child of the sender's span and current while the
/// handler runs, for a plain actor and a persistent one alike.
/// </summary>
public sealed class ConsumerSpanTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Envelopes.Serializer), nameof(ConsumerSpanTests), output)
{
    private static readonly ActivitySource Source = new("Ohmline.Telemetry.Tests.Consumer");

    public static TheoryData<string> Actors => new(nameof(PlainEcho), nameof(PersistentEcho));

    [Theory]
    [MemberData(nameof(Actors))]
    public async Task Given_a_traced_message_then_it_is_processed_in_a_child_consumer_span(
        string actor)
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var echo = await StartedAsync(actor);
        using var request = Source.StartActivity("POST /journeys")!;

        var seen = await echo.AskTraced<Seen>(new WhatIsCurrent("hello"), RemainingOrDefault, Ct);

        var process = await recorder.SingleAsync(request.TraceId, ActorShellDiagnostics.Name);
        process.DisplayName.ShouldBe($"process {actor}");
        process.Kind.ShouldBe(ActivityKind.Consumer);
        process.ParentSpanId.ShouldBe(request.SpanId);
        process.HasRemoteParent.ShouldBeTrue();
        seen.ActivityId.ShouldBe(process.Id);
        seen.Text.ShouldBe("hello");
    }

    [Fact]
    public async Task Given_a_traced_message_then_its_span_is_tagged_by_the_messaging_conventions()
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var echo = await StartedAsync(nameof(PlainEcho));
        using var request = Source.StartActivity("POST /journeys")!;

        await echo.AskTraced<Seen>(new WhatIsCurrent("hello"), RemainingOrDefault, Ct);

        var process = await recorder.SingleAsync(request.TraceId, ActorShellDiagnostics.Name);
        var tags = process.Tags.ToDictionary();
        tags["messaging.system.name"].ShouldBe("akka");
        tags["messaging.operation.type"].ShouldBe("process");
        tags["messaging.operation.name"].ShouldBe("process");
        tags["messaging.destination.name"].ShouldBe(nameof(PlainEcho));
        tags["akka.message.type"].ShouldBe(nameof(WhatIsCurrent));
        tags["akka.actor.path"].ShouldBe(echo.Path.ToString());
    }

    [Fact]
    public async Task Given_a_handler_that_throws_then_the_span_records_the_error()
    {
        using var recorder = new SpanRecorder(Source.Name, ActorShellDiagnostics.Name);
        var echo = await StartedAsync(nameof(PlainEcho));
        using (Source.StartActivity("POST /journeys"))
        {
            echo.TellTraced(new Fail("the meter is unreadable"));
        }

        await AwaitAssertAsync(
            () => recorder.Failed.ShouldContain(span =>
                span.DisplayName == $"process {nameof(PlainEcho)}"
                && span.StatusDescription == "the meter is unreadable"),
            cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_plain_message_then_no_span_is_recorded_and_none_is_current()
    {
        using var recorder = new SpanRecorder(ActorShellDiagnostics.Name);
        var echo = await StartedAsync(nameof(PersistentEcho));

        var seen = await echo.Ask<Seen>(new WhatIsCurrent("hello"), RemainingOrDefault, Ct);

        seen.ActivityId.ShouldBeNull();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// The actor, answering already: a persistent actor stashes what arrives while it recovers,
    /// and a stashed message passes through the shell twice (section 17.6).
    /// </summary>
    private async Task<IActorRef> StartedAsync(string actor)
    {
        var echo = Sys.ActorOf(
            actor == nameof(PlainEcho)
                ? Props.Create(() => new PlainEcho())
                : Props.Create(() => new PersistentEcho()),
            "echo");
        await echo.Ask<Seen>(new WhatIsCurrent("ready?"), RemainingOrDefault, Ct);
        return echo;
    }

    private sealed record WhatIsCurrent(string Text);

    private sealed record Fail(string Reason);

    private sealed record Seen(string Text, string? ActivityId);

    private sealed class PlainEcho : TracedReceiveActor
    {
        public PlainEcho()
        {
            Receive<WhatIsCurrent>(question =>
                Sender.Tell(new Seen(question.Text, Activity.Current?.Id)));
            Receive<Fail>(fail => throw new InvalidOperationException(fail.Reason));
        }
    }

    private sealed class PersistentEcho : TracedPersistentActor
    {
        public PersistentEcho() =>
            Command<WhatIsCurrent>(question =>
                Sender.Tell(new Seen(question.Text, Activity.Current?.Id)));

        public override string PersistenceId => "echo";
    }
}
