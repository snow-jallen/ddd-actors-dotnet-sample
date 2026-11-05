using System.Collections.Immutable;
using Akka.TestKit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.App;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Replies;
using Ohmline.Charging.Client.Contracts.Requests;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Api.Tests.App;

/// <summary>
/// The driver app's commands (section 19.2): the published language in, Charging's commands
/// out to the regions, and every answer a 202 with the app's command id or a problem with a
/// code.
/// </summary>
public sealed class DriverAppCommandsTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    private static readonly ChargePointId Charger = new("OHM-SLC-0042");

    private static readonly Guid CommandId = Guid.CreateVersion7(Morning);

    private static readonly Guid JourneyId = Guid.CreateVersion7(Morning.AddSeconds(1));

    private static readonly StartChargingRequest Start =
        new(CommandId, JourneyId, "OHM-SLC-0042/1", "04a2b3c4d5e6f7", 40m, "USD");

    private static readonly ChargePointStatus Registered = new(
        Charger,
        Version: 1,
        new SiteId("slc-depot"),
        ImmutableArray.Create(new ConnectorStatus(1, new Power(22m), ConnectorStatus.Available)));

    [Fact]
    public async Task Given_a_start_from_the_app_then_its_ids_and_spend_cap_reach_the_journey()
    {
        var chargePoints = CreateTestProbe();
        var journeys = CreateTestProbe();

        var answer = StartAsync(Start, chargePoints, journeys);
        await AnswerAsync<GetChargePointStatus>(chargePoints, Registered);
        var start = await AnswerAsync<StartCharging>(journeys, new CommandAccepted(1));

        start.CommandId.ShouldBe(new CommandId(CommandId));
        start.JourneyId.ShouldBe(new JourneyId(JourneyId));
        start.ConnectorId.ShouldBe(new ConnectorId(Charger, 1));
        start.TokenUid.ShouldBe(new TokenUid("04A2B3C4D5E6F7"));
        start.Hold.ShouldBe(new Money(40m, Currency.Usd));
        var accepted = (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedDto>>();
        accepted.Value.ShouldBe(new CommandAcceptedDto(CommandId, 1));
        accepted.Location.ShouldBe($"/journeys/{JourneyId}");
    }

    [Fact]
    public async Task Given_fields_that_are_not_valid_then_422_with_the_code_and_each_field()
    {
        var chargePoints = CreateTestProbe();
        var notValid = Start with
        {
            CommandId = Guid.NewGuid(), SpendCapAmount = 0m, Currency = "EUR",
        };

        var answer = await StartAsync(notValid, chargePoints, CreateTestProbe());

        var problem = Problem(answer.Result, StatusCodes.Status422UnprocessableEntity);
        problem.ProblemDetails.Extensions["code"].ShouldBe(RejectionCodes.InvalidRequest);
        problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors.Keys
            .ShouldBe(["commandId", "currency", "spendCapAmount"], ignoreOrder: true);
        await chargePoints.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
    }

    [Fact]
    public async Task Given_a_connector_no_charge_point_has_then_422_unknown_connector()
    {
        var chargePoints = CreateTestProbe();
        var journeys = CreateTestProbe();
        var elsewhere = Start with { ConnectorId = "OHM-SLC-0042/3" };

        var answer = StartAsync(elsewhere, chargePoints, journeys);
        await AnswerAsync<GetChargePointStatus>(chargePoints, Registered);

        Problem((await answer).Result, StatusCodes.Status422UnprocessableEntity)
            .ProblemDetails.Extensions["code"].ShouldBe(RejectionCodes.UnknownConnector);
        await journeys.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
    }

    [Fact]
    public async Task Given_a_stop_from_the_app_then_the_session_is_told_with_the_apps_id()
    {
        var sessions = CreateTestProbe();
        var sessionId = Guid.CreateVersion7(Morning);

        var answer = DriverAppCommands.StopChargingAsync(
            sessionId,
            new StopChargingRequest(CommandId),
            RegionOf<ChargingSessionRegion>(sessions),
            Options,
            Ct);
        var stop = await AnswerAsync<StopSession>(sessions, new CommandAccepted(3));

        stop.ShouldBe(new StopSession(new CommandId(CommandId), new SessionId(sessionId)));
        var accepted = (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedDto>>();
        accepted.Value.ShouldBe(new CommandAcceptedDto(CommandId, 3));
        accepted.Location.ShouldBe($"/sessions/{sessionId}?minVersion=3");
    }

    [Fact]
    public async Task Given_a_reservation_the_charge_point_refuses_then_409_with_its_code()
    {
        var chargePoints = CreateTestProbe();

        var answer = DriverAppCommands.ReserveConnectorAsync(
            "OHM-SLC-0042",
            1,
            new ReserveConnectorRequest(CommandId, "04A2B3C4D5E6F7"),
            RegionOf<ChargePointRegion>(chargePoints),
            Options,
            Ct);
        var reserve = await AnswerAsync<ReserveConnector>(
            chargePoints,
            new CommandRejected(
                RejectionCodes.ReservedForAnotherToken, "Connector 1 is reserved."));

        reserve.CommandId.ShouldBe(new CommandId(CommandId));
        Problem((await answer).Result, StatusCodes.Status409Conflict)
            .ProblemDetails.Extensions["code"].ShouldBe(RejectionCodes.ReservedForAnotherToken);
    }

    [Fact]
    public async Task Given_no_answer_in_time_then_504_which_the_app_retries()
    {
        var answer = await DriverAppCommands.StopChargingAsync(
            Guid.CreateVersion7(Morning),
            new StopChargingRequest(CommandId),
            RegionOf<ChargingSessionRegion>(CreateTestProbe()),
            Options,
            Ct);

        Problem(answer.Result, StatusCodes.Status504GatewayTimeout);
    }

    private Task<Results<Accepted<CommandAcceptedDto>, ProblemHttpResult>> StartAsync(
        StartChargingRequest request, TestProbe chargePoints, TestProbe journeys) =>
        DriverAppCommands.StartChargingAsync(
            request,
            RegionOf<ChargePointRegion>(chargePoints),
            RegionOf<ChargingJourneyRegion>(journeys),
            Options,
            Ct);
}
