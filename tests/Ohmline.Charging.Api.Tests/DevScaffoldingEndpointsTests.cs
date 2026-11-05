using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.Development;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Tests;

public sealed class DevScaffoldingEndpointsTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    [Fact]
    public async Task Given_a_pretend_plug_in_then_the_charge_point_hears_of_the_session()
    {
        var region = CreateTestProbe();
        var session = Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001");

        var answer = DevScaffoldingEndpoints.PlugInAsync(
            new DevScaffoldingEndpoints.Charger(
                "OHM-SLC-0042", 1, RegionOf<ChargePointRegion>(region), Clock, Options, Ct),
            new DevScaffoldingEndpoints.PlugIn(session, "04A2B3C4D5E6F7"));
        var occupy = await AnswerAsync<OccupyConnector>(region, new CommandAccepted(3));

        occupy.ShouldBe(new OccupyConnector(
            occupy.CommandId,
            new ChargePointId("OHM-SLC-0042"),
            1,
            new SessionId(session),
            new TokenUid("04A2B3C4D5E6F7")));
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>();
    }
}
