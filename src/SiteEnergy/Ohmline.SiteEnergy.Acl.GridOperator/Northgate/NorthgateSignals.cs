using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Ohmline.SiteEnergy.Acl.GridOperator.Northgate;

// Northgate Grid's demand-response feed in Northgate's words, in the style of OpenADR 2.0b.
// Nothing outside this project sees these types.

/// <summary>The revisions after the sequence asked for, and the last sequence there is.</summary>
public sealed record NorthgateSignals(
    [property: JsonPropertyName("events")] ImmutableArray<DemandResponseEvent> Events,
    [property: JsonPropertyName("latest")] long Latest);

/// <summary>
/// One revision of an event: which VENs (Northgate's ids for its customers' sites) it targets,
/// its status (<c>far</c>, <c>near</c>, <c>active</c>, <c>completed</c>, <c>cancelled</c>), and
/// its signals.
/// </summary>
public sealed record DemandResponseEvent(
    [property: JsonPropertyName("eventID")] string EventId,
    [property: JsonPropertyName("modificationNumber")] int ModificationNumber,
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("eventStatus")] string EventStatus,
    [property: JsonPropertyName("targets")] Targets Targets,
    [property: JsonPropertyName("signals")] ImmutableArray<Signal> Signals);

public sealed record Targets([property: JsonPropertyName("venID")] ImmutableArray<string> VenIds);

public sealed record Signal(
    [property: JsonPropertyName("signalName")] string SignalName,
    [property: JsonPropertyName("signalType")] string SignalType,
    [property: JsonPropertyName("units")] string Units,
    [property: JsonPropertyName("intervals")] ImmutableArray<Interval> Intervals);

/// <summary>From <c>dtstart</c>, for an ISO 8601 <c>duration</c> such as <c>PT2H</c>.</summary>
public sealed record Interval(
    [property: JsonPropertyName("dtstart")] DateTimeOffset Start,
    [property: JsonPropertyName("duration")] string Duration,
    [property: JsonPropertyName("payload")] decimal Payload);
