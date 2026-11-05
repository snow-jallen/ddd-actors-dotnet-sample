using Akka.Actor;
using Akka.Event;

namespace Ohmline.ActorShell.Telemetry;

/// <summary>
/// Counts every dead letter by the type of the message (section 18.2): a reply to an asker that
/// gave up, a command to an entity that stopped with commands in its stash, a message to a node
/// that left. Akka.NET logs only the first few; the event stream publishes them all.
/// </summary>
/// <remarks>
/// Only <see cref="DeadLetter"/>: a <see cref="SuppressedDeadLetter"/> is one that Akka.NET, or
/// the sender, expected to be lost, and <see cref="Dropped"/> is a message refused on purpose.
/// </remarks>
public sealed class DeadLetterMetricsListener : ReceiveActor
{
    public DeadLetterMetricsListener(ActorShellMetrics metrics) =>
        Receive<DeadLetter>(letter => metrics.DeadLetter(letter.Message));

    protected override void PreStart() =>
        Context.System.EventStream.Subscribe<DeadLetter>(Self);

    protected override void PostStop() => Context.System.EventStream.Unsubscribe(Self);
}
