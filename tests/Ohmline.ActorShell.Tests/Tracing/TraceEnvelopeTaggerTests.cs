using Akka.Persistence.Journal;
using Ohmline.ActorShell.Tests.Messaging;
using Ohmline.ActorShell.Tracing;

namespace Ohmline.ActorShell.Tests.Tracing;

public sealed class TraceEnvelopeTaggerTests
{
    private const string TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private static readonly SignalTaggers Taggers = new();

    [Fact]
    public void Given_a_traced_event_a_tagger_knows_then_the_envelope_gets_its_tags()
    {
        var envelope = new TraceEnvelope(new Ping("hello"), TraceParent, null);

        var tagged = Taggers.ToJournal(envelope).ShouldBeOfType<Tagged>();

        tagged.Payload.ShouldBeSameAs(envelope);
        tagged.Tags.ShouldBe(["pings"]);
    }

    [Fact]
    public void Given_a_traced_event_no_tagger_knows_then_the_envelope_goes_untagged()
    {
        var envelope = new TraceEnvelope("a stranger", TraceParent, null);

        Taggers.ToJournal(envelope).ShouldBeSameAs(envelope);
    }

    [Fact]
    public void Given_anything_but_an_envelope_then_it_is_left_alone()
    {
        var ping = new Ping("hello");

        Taggers.ToJournal(ping).ShouldBeSameAs(ping);
    }

    /// <summary>Tags pings, as a context's tagger tags its own events.</summary>
    private sealed class PingTagger : IWriteEventAdapter
    {
        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt) => evt is Ping ? new Tagged(evt, ["pings"]) : evt;
    }

    /// <summary>Tags nothing, as Tariffs' events go untagged.</summary>
    private sealed class NoTagger : IWriteEventAdapter
    {
        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt) => evt;
    }

    private sealed class SignalTaggers() : TraceEnvelopeTagger(new NoTagger(), new PingTagger());
}
