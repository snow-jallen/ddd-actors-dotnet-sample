using System.Diagnostics;
using Akka.Persistence.Query;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Actors.Diagnostics;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// The span a projection processes a traced event in (section 17.7). The event was stored in a
/// trace envelope by the command that decided it; the projection reads it seconds or days
/// later, and answers no one, so its span is not that command's child: it starts a trace of
/// its own, with a link to the command's span. Whatever the projection asks or tells then
/// belongs to the new trace.
/// </summary>
internal static class ProjectionSpans
{
    /// <summary>
    /// The span, current from here on, or null when the event is not traced or nothing listens
    /// to <c>Ohmline.Charging</c>.
    /// </summary>
    public static Activity? Start(ProjectionStream stream, EventEnvelope envelope)
    {
        if (envelope.Event is not TraceEnvelope traced)
        {
            return null;
        }

        // A root, whatever the stream's thread had current: the write is a link, not a parent.
        Activity.Current = null;
        var span = ChargingDiagnostics.ActivitySource.StartActivity(
            $"process {stream.ProjectionId}",
            ActivityKind.Consumer,
            parentContext: default,
            links: [new ActivityLink(traced.ParentContext)]);
        if (span is { IsAllDataRequested: true })
        {
            span.SetTag("messaging.system.name", "akka");
            span.SetTag("messaging.operation.type", "process");
            span.SetTag("messaging.operation.name", "process");
            span.SetTag("messaging.destination.name", stream.Tag);
            span.SetTag("akka.persistence.id", envelope.PersistenceId);
            span.SetTag("akka.persistence.sequence_nr", envelope.SequenceNr);
            span.SetTag("akka.event.type", MessageNames.Of(traced.Message));
        }

        return span;
    }

    /// <summary>The envelope with the stored event out of its trace envelope.</summary>
    public static EventEnvelope Opened(EventEnvelope envelope) =>
        envelope.Event is TraceEnvelope traced
            ? new EventEnvelope(
                envelope.Offset,
                envelope.PersistenceId,
                envelope.SequenceNr,
                traced.Message,
                envelope.Timestamp,
                envelope.Tags)
            : envelope;
}
