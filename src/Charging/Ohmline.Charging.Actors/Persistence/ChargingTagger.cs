using Akka.Persistence.Journal;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.Domain.Listings;
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Actors.Persistence;

/// <summary>
/// Tags every event Charging persists on its way into the journal, by the event's family. The
/// actors persist plain events and never see a tag; the journal stores the tags in a table of
/// their own, which is what <c>EventsByTag</c> reads (section 13.3).
/// </summary>
public sealed class ChargingTagger : IWriteEventAdapter
{
    /// <summary>The adapter's name in the journal's configuration.</summary>
    public const string Name = "charging-tagger";

    /// <summary>The event families the journal passes through this adapter.</summary>
    public static IReadOnlyList<Type> BoundTypes { get; } =
    [
        typeof(ChargePointEvent),
        typeof(ChargePointListingEvent),
        typeof(SessionEvent),
        typeof(JourneyEvent),
    ];

    /// <summary>No manifest of its own: the serializer's manifest is the one that counts.</summary>
    public string Manifest(object evt) => string.Empty;

    public object ToJournal(object evt) => evt switch
    {
        ChargePointEvent or ChargePointListingEvent => new Tagged(evt, [ChargingTags.Charging]),
        SessionEvent => new Tagged(evt, [ChargingTags.ChargingSession]),
        JourneyEvent => new Tagged(evt, [ChargingTags.ChargingJourney]),
        _ => evt,
    };
}
