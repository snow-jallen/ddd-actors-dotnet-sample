using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Domain.Listings;

/// <summary>
/// What can happen to a charge point's listing. Written to the journal, so plain data only.
/// Every event records the command that caused it and when it happened.
/// </summary>
public closed record ChargePointListingEvent(ChargePointId ChargePointId)
{
    /// <summary>The command that caused this event: its causation.</summary>
    public CommandId CommandId { get; init; }

    /// <summary>When it happened: the time of the decision, from its context.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// The listing was replaced. The event carries the whole new listing, not a change to the old
/// one: the listing is state sourced (section 11.7), so the latest event alone is the state,
/// and the events before it may be deleted.
/// </summary>
public sealed record ChargePointListingRevised(
    ChargePointId ChargePointId,
    string DisplayName,
    string AccessInstructions,
    string OpeningHours,
    long Revision)
    : ChargePointListingEvent(ChargePointId);
