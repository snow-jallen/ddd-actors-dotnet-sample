using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Actors.Tests.Sharding;

/// <summary>
/// Every command and query Charging owns reaches exactly one region, as the entity whose id it
/// carries. A session's events reach the journey its session is named after; nothing else
/// Charging sends (other events, replies, views) is routed at all.
/// </summary>
public sealed class ChargingMessageExtractorsTests
{
    private static readonly (string Region, EntityMessageExtractor Extractor)[] Regions =
    [
        ("charge-point", ChargingMessageExtractors.ChargePoints),
        ("charging-session", ChargingMessageExtractors.Sessions),
        ("charge-point-listing", ChargingMessageExtractors.Listings),
        ("charging-journey", ChargingMessageExtractors.Journeys),
    ];

    public static TheoryData<string> Samples =>
        new(ChargingSamples.All.Select(sample => sample.GetType().Name).Distinct());

    [Theory]
    [MemberData(nameof(Samples))]
    public void Given_a_message_then_only_the_region_of_its_aggregate_routes_it(string type)
    {
        var message = ChargingSamples.All.First(sample => sample.GetType().Name == type);

        var routed = Regions
            .Where(region => region.Extractor.EntityId(message) is not null)
            .Select(region => (region.Region, region.Extractor.EntityId(message)));

        routed.ShouldBe(Expected(message));
    }

    private static IEnumerable<(string, string?)> Expected(object message) => message switch
    {
        ChargePointCommand command => [("charge-point", command.ChargePointId.Value)],
        ChargePointQuery query => [("charge-point", query.ChargePointId.Value)],
        SessionCommand command => [("charging-session", command.SessionId.ToString())],
        ChargePointListingCommand command =>
            [("charge-point-listing", command.ChargePointId.Value)],
        ChargePointListingQuery query => [("charge-point-listing", query.ChargePointId.Value)],
        JourneyCommand command => [("charging-journey", command.JourneyId.ToString())],
        JourneyQuery query => [("charging-journey", query.JourneyId.ToString())],
        SessionEvent @event => [("charging-journey", @event.SessionId.ToString())],
        _ => [],
    };
}
