using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Api.Tests;

public sealed class ChargePointEndpointsTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    private static readonly ChargePointId Charger = new("OHM-SLC-0042");

    private static readonly RegisterChargePointRequest Registration =
        new("OHM-SLC-0042", "slc-depot", [22m, 22m]);

    [Fact]
    public async Task Given_a_registration_then_the_region_is_told_and_the_answer_is_202()
    {
        var region = CreateTestProbe();

        var answer = ChargePointEndpoints.RegisterAsync(
            Registration, RegionOf<ChargePointRegion>(region), Clock, Options, Ct);
        var command = await AnswerAsync<RegisterChargePoint>(region, new CommandAccepted(1));

        command.ChargePointId.ShouldBe(Charger);
        command.SiteId.ShouldBe(new SiteId("slc-depot"));
        command.ConnectorMaxPowers.ShouldBe([new Power(22m), new Power(22m)]);
        var accepted = (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>();
        accepted.Location.ShouldBe("/charge-points/OHM-SLC-0042");
        accepted.Value.ShouldBe(new CommandAcceptedResponse(1));
    }

    [Fact]
    public async Task Given_fields_that_are_not_valid_then_422_names_each_and_nothing_is_sent()
    {
        var region = CreateTestProbe();

        var answer = await ChargePointEndpoints.RegisterAsync(
            new RegisterChargePointRequest("OHM-0042", "Depot!", [22m, -1m]),
            RegionOf<ChargePointRegion>(region),
            Clock,
            Options,
            Ct);

        var problem = Problem(answer.Result, StatusCodes.Status422UnprocessableEntity);
        problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors.Keys
            .ShouldBe(["chargePointId", "siteId", "connectorMaxPowersKw[1]"], ignoreOrder: true);
        await region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
    }

    [Fact]
    public async Task Given_a_charge_point_without_connectors_then_the_registration_is_not_valid()
    {
        var answer = await ChargePointEndpoints.RegisterAsync(
            Registration with { ConnectorMaxPowersKw = [] },
            RegionOf<ChargePointRegion>(CreateTestProbe()),
            Clock,
            Options,
            Ct);

        Problem(answer.Result, StatusCodes.Status422UnprocessableEntity)
            .ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors.Keys
            .ShouldBe(["connectorMaxPowersKw"]);
    }

    [Fact]
    public async Task Given_a_rejected_command_then_409_carries_the_rejection_code()
    {
        var region = CreateTestProbe();

        var answer = ChargePointEndpoints.RegisterAsync(
            Registration, RegionOf<ChargePointRegion>(region), Clock, Options, Ct);
        await AnswerAsync<RegisterChargePoint>(
            region,
            new CommandRejected("already-registered", "OHM-SLC-0042 is already registered."));

        var problem = Problem((await answer).Result, StatusCodes.Status409Conflict);
        problem.ProblemDetails.Detail.ShouldBe("OHM-SLC-0042 is already registered.");
        problem.ProblemDetails.Extensions["code"].ShouldBe("already-registered");
    }

    [Fact]
    public async Task Given_no_answer_in_time_then_504_and_the_command_may_still_happen()
    {
        var answer = await ChargePointEndpoints.RegisterAsync(
            Registration, RegionOf<ChargePointRegion>(CreateTestProbe()), Clock, Options, Ct);

        Problem(answer.Result, StatusCodes.Status504GatewayTimeout).ProblemDetails.Detail
            .ShouldBe("No answer within 3 s. The command may still have been carried out.");
    }

    [Fact]
    public async Task Given_a_reservation_then_the_token_is_read_in_upper_case()
    {
        var region = CreateTestProbe();

        var answer = ChargePointEndpoints.ReserveAsync(
            "OHM-SLC-0042",
            1,
            new ReservationRequest("04a2b3c4d5e6f7"),
            RegionOf<ChargePointRegion>(region),
            Clock,
            Options,
            Ct);
        var command = await AnswerAsync<ReserveConnector>(region, new CommandAccepted(2));

        command.ShouldBe(new ReserveConnector(
            command.CommandId, Charger, 1, new TokenUid("04A2B3C4D5E6F7")));
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>()
            .Value.ShouldBe(new CommandAcceptedResponse(2));
    }

    [Fact]
    public async Task Given_a_route_that_names_no_charge_point_then_404()
    {
        var answer = await ChargePointEndpoints.ReserveAsync(
            "slc-depot",
            1,
            new ReservationRequest("04A2B3C4D5E6F7"),
            RegionOf<ChargePointRegion>(CreateTestProbe()),
            Clock,
            Options,
            Ct);

        Problem(answer.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Given_a_registered_charge_point_when_read_then_200_with_its_connectors()
    {
        var region = CreateTestProbe();

        var answer = ChargePointEndpoints.GetAsync(
            "OHM-SLC-0042", RegionOf<ChargePointRegion>(region), Options, Ct);
        await AnswerAsync<GetChargePointStatus>(region, new ChargePointStatus(
            Charger,
            2,
            new SiteId("slc-depot"),
            [
                new ConnectorStatus(
                    1,
                    new Power(22m),
                    ConnectorStatus.Reserved,
                    new TokenUid("04A2B3C4D5E6F7"),
                    Morning.AddMinutes(15)),
                new ConnectorStatus(2, new Power(22m), ConnectorStatus.Available),
            ]));

        var ok = (await answer).Result.ShouldBeOfType<Ok<ChargePointResponse>>();
        ok.Value.ShouldNotBeNull().ChargePointId.ShouldBe("OHM-SLC-0042");
        ok.Value.Version.ShouldBe(2);
        ok.Value.SiteId.ShouldBe("slc-depot");
        ok.Value.Connectors.ShouldBe(
        [
            new ConnectorResponse(
                1, 22m, "reserved", "04A2B3C4D5E6F7", Morning.AddMinutes(15), null, null),
            new ConnectorResponse(2, 22m, "available", null, null, null, null),
        ]);
    }

    [Fact]
    public async Task Given_a_charge_point_never_registered_when_read_then_404()
    {
        var region = CreateTestProbe();

        var answer = ChargePointEndpoints.GetAsync(
            "OHM-SLC-0099", RegionOf<ChargePointRegion>(region), Options, Ct);
        await AnswerAsync<GetChargePointStatus>(
            region, new ChargePointStatus(new ChargePointId("OHM-SLC-0099"), 0, null, []));

        Problem((await answer).Result, StatusCodes.Status404NotFound).ProblemDetails.Detail
            .ShouldBe("Charge point OHM-SLC-0099 is not registered.");
    }
}
