using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.App;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Tests.App;

public sealed class DriverAppListingTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    [Fact]
    public async Task Given_a_listing_then_the_app_reads_it_in_its_published_language()
    {
        var listings = CreateTestProbe();
        var chargePoint = new ChargePointId("OHM-SLC-0042");

        var answer = DriverAppEndpoints.GetListingAsync(
            "OHM-SLC-0042", RegionOf<ChargePointListingRegion>(listings), Options, Ct);
        await AnswerAsync<GetChargePointListing>(
            listings,
            new ChargePointListingView(chargePoint, 3, "Salt Lake bus depot, public bays"));

        (await answer).Result.ShouldBeOfType<Ok<ChargePointListingDto>>().Value.ShouldBe(
            new ChargePointListingDto(
                "OHM-SLC-0042", 3, "Salt Lake bus depot, public bays", "", ""));
    }
}
