using System.Text.Json.Serialization;

namespace Ohmline.Charging.Contracts.Integration;

/// <summary>
/// What Charging tells the rest of Ohmline, in its Published Language (section 16.1): facts
/// other services can act on, and nothing about how Charging decided them. Not a domain event:
/// a domain event is Charging's own record, free to change with its model; an integration event
/// is a promise to every consumer, versioned by its wire name and never changed in place.
/// </summary>
/// <remarks>
/// The wire name of each event is written down here, never derived from the C# name, so a type
/// can be renamed or moved and its consumers never notice. A closed hierarchy, because it is
/// serialized (ADR 0003).
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SessionCompleted), SessionCompleted.WireName)]
[JsonDerivedType(typeof(SessionCompletedV2), SessionCompletedV2.WireName)]
public closed record ChargingIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt);

/// <summary>
/// A charging session is over and priced: what Billing invoices and what a receipt shows. The
/// event id is the same every time the same session is published, so a consumer that reads it
/// twice can tell.
/// </summary>
public sealed record SessionCompleted(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid SessionId,
    string TokenUid,
    string ConnectorId,
    decimal EnergyKwh,
    PriceBreakdown Price,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt)
    : ChargingIntegrationEvent(EventId, OccurredAt)
{
    public const string WireName = "ohmline.charging.session-completed.v1";
}

/// <summary>
/// The second version of <see cref="SessionCompleted"/> (section 20.9): the connector as its
/// charge point and number instead of a string each consumer had to take apart, and the site.
/// A shape that changed is a new wire name, never an edit of the old: Charging publishes both
/// for as long as a consumer still reads the first, and each has an event id of its own.
/// </summary>
public sealed record SessionCompletedV2(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid SessionId,
    string SiteId,
    string TokenUid,
    string ChargePointId,
    int ConnectorNumber,
    decimal EnergyKwh,
    PriceBreakdown Price,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt)
    : ChargingIntegrationEvent(EventId, OccurredAt)
{
    public const string WireName = "ohmline.charging.session-completed.v2";
}
