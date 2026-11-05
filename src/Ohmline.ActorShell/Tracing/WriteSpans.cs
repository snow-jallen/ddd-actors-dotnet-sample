using System.Diagnostics;
using Ohmline.ActorShell.Diagnostics;

namespace Ohmline.ActorShell.Tracing;

/// <summary>
/// The spans of one Persist or PersistAll (section 17.6), children of the span that was current
/// when it was called. Akka.NET runs the callbacks later, from the journal's reply, when that
/// span has long ended and nothing is current; so each event's <c>persist</c> span is recorded
/// afterwards, from an explicit start time, and an <c>apply</c> span is made current around
/// each callback, so that what it logs and sends stays in the trace.
/// </summary>
internal sealed class WriteSpans
{
    private readonly ActivityContext _parent;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly string _actorType;
    private readonly string _persistenceId;

    private WriteSpans(ActivityContext parent, string actorType, string persistenceId)
    {
        _parent = parent;
        _actorType = actorType;
        _persistenceId = persistenceId;
    }

    /// <summary>The write's spans, or null when it is untraced: it records nothing.</summary>
    public static WriteSpans? Begin(string actorType, string persistenceId) =>
        Activity.Current is { } current
            ? new WriteSpans(current.Context, actorType, persistenceId)
            : null;

    /// <summary>
    /// The event is in the journal: a span from the moment Persist was called until now, which
    /// ends as soon as it is recorded, so it is never current.
    /// </summary>
    public void Stored(object @event, long sequenceNr)
    {
        using var persist = ActorShellDiagnostics.ActivitySource.StartActivity(
            $"persist {MessageNames.Of(@event)}",
            ActivityKind.Internal,
            _parent,
            startTime: _startedAt);
        persist?.SetTag("akka.persistence.id", _persistenceId);
        persist?.SetTag("akka.persistence.sequence_nr", sequenceNr);
        persist?.SetTag("akka.event.type", MessageNames.Of(@event));
    }

    /// <summary>The span around the callback that applies one stored event.</summary>
    public Activity? Applying(object @event) =>
        ActorShellDiagnostics.ActivitySource.StartActivity(
            $"apply {MessageNames.Of(@event)}", ActivityKind.Internal, _parent);

    /// <summary>The span around PersistAll's last callback: every event is stored.</summary>
    public Activity? Completing() =>
        ActorShellDiagnostics.ActivitySource.StartActivity(
            $"persisted {_actorType}", ActivityKind.Internal, _parent);
}
