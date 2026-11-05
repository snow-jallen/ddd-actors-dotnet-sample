using System.Diagnostics;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.ActorShell.Tracing;
using Ohmline.Testing;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// The envelope carries what the mailbox drops (section 17.4): told or asked with the traced
/// variants from inside a span, the message arrives with the sender's traceparent, serialized
/// and read back on the way, as between two nodes. The actor still has no current span; making
/// one from the envelope is the shell's job (section 17.5).
/// </summary>
public sealed class TraceEnvelopeTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Envelopes.Serializer), nameof(TraceEnvelopeTests), output)
{
    private static readonly ActivitySource Source = new("Ohmline.Telemetry.Tests.Envelope");

    [Fact]
    public async Task Given_a_span_when_told_traced_then_the_message_arrives_with_its_traceparent()
    {
        using var recorder = new SpanRecorder(Source.Name);
        var witness = Sys.ActorOf(Witness.Props, "witness");
        using var request = Source.StartActivity("POST /journeys");

        witness.TellTraced(new WhatArrived(), TestActor);

        var arrived = await ExpectMsgAsync<Arrived>(cancellationToken: Ct);
        arrived.TraceParent.ShouldBe(request!.Id);
        arrived.CurrentActivityId.ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_span_when_asked_traced_then_the_question_carries_its_traceparent()
    {
        using var recorder = new SpanRecorder(Source.Name);
        var witness = Sys.ActorOf(Witness.Props, "witness");
        using var request = Source.StartActivity("GET /journeys/{id}");

        var arrived = await witness.AskTraced<Arrived>(new WhatArrived(), RemainingOrDefault, Ct);

        arrived.TraceParent.ShouldBe(request!.Id);
    }

    [Fact]
    public async Task Given_no_span_when_told_traced_then_the_message_arrives_plain()
    {
        var witness = Sys.ActorOf(Witness.Props, "witness");

        witness.TellTraced(new WhatArrived(), TestActor);

        (await ExpectMsgAsync<Arrived>(cancellationToken: Ct)).TraceParent.ShouldBeNull();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record WhatArrived;

    private sealed record Arrived(string? TraceParent, string? CurrentActivityId);

    /// <summary>Says whether its message came in an envelope, and what span was current.</summary>
    private sealed class Witness : ReceiveActor
    {
        public Witness()
        {
            Receive<TraceEnvelope>(envelope =>
                Sender.Tell(new Arrived(envelope.TraceParent, Activity.Current?.Id)));
            Receive<WhatArrived>(_ => Sender.Tell(new Arrived(null, Activity.Current?.Id)));
        }

        public static Props Props { get; } = Akka.Actor.Props.Create(() => new Witness());
    }
}
