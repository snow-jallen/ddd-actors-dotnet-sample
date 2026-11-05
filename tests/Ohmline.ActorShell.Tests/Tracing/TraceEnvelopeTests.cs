using System.Diagnostics;
using Ohmline.ActorShell.Tests.Messaging;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.ActorShell.Tests.Tracing;

public sealed class TraceEnvelopeTests
{
    [Fact]
    public void Given_no_current_span_when_wrapped_then_the_message_goes_alone()
    {
        Activity.Current = null;
        var ping = new Ping("hello");

        TraceEnvelope.Wrap(ping).ShouldBeSameAs(ping);
    }

    [Fact]
    public void Given_a_current_span_when_wrapped_then_the_envelope_carries_its_traceparent()
    {
        using var span = new Activity("POST /journeys").Start();
        span.TraceStateString = "ohmline=edge";
        var ping = new Ping("hello");

        var envelope = TraceEnvelope.Wrap(ping).ShouldBeOfType<TraceEnvelope>();

        envelope.Message.ShouldBeSameAs(ping);
        envelope.TraceParent.ShouldBe(span.Id);
        envelope.TraceParent.ShouldStartWith($"00-{span.TraceId}-{span.SpanId}-");
        envelope.TraceState.ShouldBe("ohmline=edge");
    }

    [Fact]
    public void Given_an_envelope_when_wrapped_again_then_it_is_not_wrapped_twice()
    {
        using var span = new Activity("POST /journeys").Start();
        var envelope = TraceEnvelope.Wrap(new Ping("hello"));

        TraceEnvelope.Wrap(envelope).ShouldBeSameAs(envelope);
    }

    [Fact]
    public void Given_an_envelope_or_a_plain_message_when_unwrapped_then_the_message_comes_out()
    {
        var ping = new Ping("hello");

        TraceEnvelope.Unwrap(new TraceEnvelope(ping, "00-x-y-01", null)).ShouldBeSameAs(ping);
        TraceEnvelope.Unwrap(ping).ShouldBeSameAs(ping);
    }

    [Fact]
    public void Given_an_envelope_then_its_parent_context_is_the_senders_span_seen_from_afar()
    {
        using var span = new Activity("POST /journeys").Start();
        var envelope = (TraceEnvelope)TraceEnvelope.Wrap(new Ping("hello"));

        var parent = envelope.ParentContext;

        parent.TraceId.ShouldBe(span.TraceId);
        parent.SpanId.ShouldBe(span.SpanId);
        parent.IsRemote.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_traceparent_that_is_not_W3C_then_the_parent_context_is_default() =>
        new TraceEnvelope(new Ping("hello"), "|abc.1.", null).ParentContext.ShouldBe(default);
}
