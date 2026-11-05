using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Actors;

/// <summary>
/// Which entity each of Charging's messages is for: a switch over Charging's own message
/// families, each of which names its aggregate, rather than an interface that every message
/// would have to implement. The same extractors serve the regions on the charging nodes and,
/// from Chapter 16, the proxies on the gateway.
/// </summary>
public static class ChargingMessageExtractors
{
    public static EntityMessageExtractor ChargePoints { get; } = new(message => message switch
    {
        ChargePointCommand command => command.ChargePointId.Value,
        ChargePointQuery query => query.ChargePointId.Value,
        _ => null,
    });

    public static EntityMessageExtractor Sessions { get; } = new(message => message switch
    {
        SessionCommand command => command.SessionId.ToString(),
        _ => null,
    });

    public static EntityMessageExtractor Listings { get; } = new(message => message switch
    {
        ChargePointListingCommand command => command.ChargePointId.Value,
        ChargePointListingQuery query => query.ChargePointId.Value,
        _ => null,
    });

    /// <summary>
    /// A journey by its id; a session's event to the journey its session is named after
    /// (section 15.2), so the session's events find their journey without a lookup.
    /// </summary>
    public static EntityMessageExtractor Journeys { get; } = new(message => message switch
    {
        JourneyCommand command => command.JourneyId.ToString(),
        JourneyQuery query => query.JourneyId.ToString(),
        SessionEvent @event => @event.SessionId.ToString(),
        _ => null,
    });
}
