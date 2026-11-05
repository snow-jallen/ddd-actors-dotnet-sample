using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Tests;

public sealed class ListingEndpointsTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    private static readonly ChargePointId Charger = new("OHM-SLC-0042");

    [Fact]
    public async Task Given_a_new_listing_then_the_whole_text_is_sent_as_one_revision()
    {
        var region = CreateTestProbe();

        var answer = ListingEndpoints.ReviseAsync(
            "OHM-SLC-0042",
            new ListingRequest("Salt Lake bus depot, public bays", "Level -2, bays 14-19", "24/7"),
            RegionOf<ChargePointListingRegion>(region),
            Clock,
            Options,
            Ct);
        var revision = await AnswerAsync<ReviseChargePointListing>(region, new CommandAccepted(1));

        revision.ShouldBe(new ReviseChargePointListing(
            revision.CommandId,
            Charger,
            "Salt Lake bus depot, public bays",
            "Level -2, bays 14-19",
            "24/7"));
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>()
            .Location.ShouldBe("/charge-points/OHM-SLC-0042/listing");
    }

    [Fact]
    public async Task Given_a_listed_charge_point_when_read_then_200_with_its_revision()
    {
        var region = CreateTestProbe();

        var answer = ListingEndpoints.GetAsync(
            "OHM-SLC-0042", RegionOf<ChargePointListingRegion>(region), Options, Ct);
        await AnswerAsync<GetChargePointListing>(
            region, new ChargePointListingView(Charger, 3, "Depot", "Level -2", "24/7"));

        (await answer).Result.ShouldBeOfType<Ok<ListingResponse>>().Value
            .ShouldBe(new ListingResponse("OHM-SLC-0042", 3, "Depot", "Level -2", "24/7"));
    }

    [Fact]
    public async Task Given_a_charge_point_never_listed_when_read_then_404()
    {
        var region = CreateTestProbe();

        var answer = ListingEndpoints.GetAsync(
            "OHM-SLC-0042", RegionOf<ChargePointListingRegion>(region), Options, Ct);
        await AnswerAsync<GetChargePointListing>(region, new ChargePointListingView(Charger, 0));

        Problem((await answer).Result, StatusCodes.Status404NotFound);
    }
}
