using System.Collections.Immutable;
using Akka.TestKit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Api.Tests;

public sealed class JourneyEndpointsTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    private static readonly ChargePointId Charger = new("OHM-SLC-0042");

    private static readonly StartChargingRequest Request =
        new("04a2b3c4d5e6f7", "OHM-SLC-0042/1");

    private static readonly ChargePointStatus Registered = new(
        Charger,
        Version: 1,
        new SiteId("slc-depot"),
        ImmutableArray.Create(
            new ConnectorStatus(1, new Power(22m), ConnectorStatus.Available),
            new ConnectorStatus(2, new Power(22m), ConnectorStatus.Available)));

    [Fact]
    public async Task Given_a_connector_then_a_journey_starts_with_its_site_power_and_the_hold()
    {
        var chargePoints = CreateTestProbe();
        var journeys = CreateTestProbe();

        var answer = Start(chargePoints, journeys, idempotencyKey: null);
        await AnswerAsync<GetChargePointStatus>(chargePoints, Registered);
        var start = await AnswerAsync<StartCharging>(journeys, new CommandAccepted(1));

        start.TokenUid.ShouldBe(new TokenUid("04A2B3C4D5E6F7"));
        start.ConnectorId.ShouldBe(new ConnectorId(Charger, 1));
        start.SiteId.ShouldBe(new SiteId("slc-depot"));
        start.MaxPower.ShouldBe(new Power(22m));
        start.Hold.ShouldBe(new Money(50m, Currency.Usd));
        var accepted = (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>();
        accepted.Location.ShouldBe($"/journeys/{start.JourneyId}");
    }

    [Fact]
    public async Task Given_the_same_idempotency_key_twice_then_both_name_the_same_journey()
    {
        var key = Guid.CreateVersion7(Morning).ToString();
        var chargePoints = CreateTestProbe();
        var journeys = CreateTestProbe();

        var first = Start(chargePoints, journeys, key);
        await AnswerAsync<GetChargePointStatus>(chargePoints, Registered);
        var started = await AnswerAsync<StartCharging>(journeys, new CommandAccepted(1));
        await first;
        var again = Start(chargePoints, journeys, key);
        await AnswerAsync<GetChargePointStatus>(chargePoints, Registered);
        var startedAgain = await AnswerAsync<StartCharging>(journeys, new CommandAccepted(1));
        await again;

        startedAgain.JourneyId.ShouldBe(started.JourneyId);
        startedAgain.CommandId.ShouldBe(started.CommandId);
    }

    [Fact]
    public async Task Given_a_connector_the_charge_point_does_not_have_then_404_and_no_journey()
    {
        var chargePoints = CreateTestProbe();
        var journeys = CreateTestProbe();

        var answer = JourneyEndpoints.StartAsync(
            Request with { ConnectorId = "OHM-SLC-0042/3" },
            null,
            RegionOf<ChargePointRegion>(chargePoints),
            RegionOf<ChargingJourneyRegion>(journeys),
            Clock,
            Options,
            Ct);
        await AnswerAsync<GetChargePointStatus>(chargePoints, Registered);

        Problem((await answer).Result, StatusCodes.Status404NotFound);
        await journeys.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
    }

    [Fact]
    public async Task Given_fields_that_are_not_valid_then_422_names_each_and_nothing_is_asked()
    {
        var chargePoints = CreateTestProbe();

        var answer = await JourneyEndpoints.StartAsync(
            new StartChargingRequest("not a token", "OHM-SLC-0042"),
            null,
            RegionOf<ChargePointRegion>(chargePoints),
            RegionOf<ChargingJourneyRegion>(CreateTestProbe()),
            Clock,
            Options,
            Ct);

        Problem(answer.Result, StatusCodes.Status422UnprocessableEntity)
            .ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors.Keys
            .ShouldBe(["tokenUid", "connectorId"], ignoreOrder: true);
        await chargePoints.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
    }

    [Fact]
    public async Task Given_a_journey_then_get_shows_its_step()
    {
        var journeys = CreateTestProbe();
        var journeyId = new JourneyId(Guid.CreateVersion7(Morning));
        var session = new SessionId(journeyId.Value);

        var answer = JourneyEndpoints.GetAsync(
            journeyId.Value, RegionOf<ChargingJourneyRegion>(journeys), Options, Ct);
        await AnswerAsync<GetJourney>(
            journeys, new JourneyView(journeyId, 2, JourneyView.AwaitingHold, session));

        (await answer).Result.ShouldBeOfType<Ok<JourneyResponse>>().Value.ShouldBe(
            new JourneyResponse(journeyId.Value, 2, "awaiting-hold", session.Value, null, null));
    }

    [Fact]
    public async Task Given_a_journey_that_never_started_then_get_is_404()
    {
        var journeys = CreateTestProbe();
        var journeyId = new JourneyId(Guid.CreateVersion7(Morning));

        var answer = JourneyEndpoints.GetAsync(
            journeyId.Value, RegionOf<ChargingJourneyRegion>(journeys), Options, Ct);
        await AnswerAsync<GetJourney>(
            journeys, new JourneyView(journeyId, 0, JourneyView.NotStarted));

        Problem((await answer).Result, StatusCodes.Status404NotFound);
    }

    private Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>> Start(
        TestProbe chargePoints,
        TestProbe journeys,
        string? idempotencyKey) =>
        JourneyEndpoints.StartAsync(
            Request,
            idempotencyKey,
            RegionOf<ChargePointRegion>(chargePoints),
            RegionOf<ChargingJourneyRegion>(journeys),
            Clock,
            Options,
            Ct);
}
