using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Ohmline.FakeGridOperator;

/// <summary>
/// A demand-response event, in the style of OpenADR 2.0b: who it targets (Northgate's own VEN
/// ids), when it is active, and how much less to draw. Every revision of an event gets the next
/// sequence number; a reader asks for everything after the last one it saw.
/// </summary>
public sealed record DemandResponseEvent(
    [property: JsonPropertyName("eventID")] string EventId,
    [property: JsonPropertyName("modificationNumber")] int ModificationNumber,
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("eventStatus")] string EventStatus,
    [property: JsonPropertyName("targets")] Targets Targets,
    [property: JsonPropertyName("signals")] ImmutableArray<Signal> Signals);

public sealed record Targets([property: JsonPropertyName("venID")] ImmutableArray<string> VenIds);

/// <summary><c>LOAD_DISPATCH</c>, as a <c>delta</c> in kW, kW being Northgate's habit.</summary>
public sealed record Signal(
    [property: JsonPropertyName("signalName")] string SignalName,
    [property: JsonPropertyName("signalType")] string SignalType,
    [property: JsonPropertyName("units")] string Units,
    [property: JsonPropertyName("intervals")] ImmutableArray<Interval> Intervals);

/// <summary>From <c>dtstart</c> for <c>duration</c> (ISO 8601, <c>PT2H</c>), the value.</summary>
public sealed record Interval(
    [property: JsonPropertyName("dtstart")] DateTimeOffset Start,
    [property: JsonPropertyName("duration")] string Duration,
    [property: JsonPropertyName("payload")] decimal Payload);

/// <summary>The events after a sequence number, and the last number there is.</summary>
public sealed record SignalsResponse(
    [property: JsonPropertyName("events")] ImmutableArray<DemandResponseEvent> Events,
    [property: JsonPropertyName("latest")] long Latest);

/// <summary>What an operator announces: a new event, or a new revision of one.</summary>
public sealed record AnnounceEvent(
    string EventId,
    string EventStatus,
    ImmutableArray<string> VenIds,
    decimal ReductionKw,
    DateTimeOffset Start,
    TimeSpan Duration);
