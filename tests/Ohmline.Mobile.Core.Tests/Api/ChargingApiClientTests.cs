using System.Net;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Mobile.Core.Api;

namespace Ohmline.Mobile.Core.Tests.Api;

public sealed class ChargingApiClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_listed_charge_point_then_its_listing_is_read_from_app_v1()
    {
        var server = new FakeServer().Answers(HttpStatusCode.OK, """
            {"chargePointId":"OHM-SLC-0042","revision":3,
             "displayName":"Salt Lake bus depot, public bays",
             "accessInstructions":"Enter from 400 South.","openingHours":"Mo-Su 05:00-23:00"}
            """);
        var api = new ChargingApiClient(server.Client());

        var listing = await api.GetListingAsync("OHM-SLC-0042", Ct);

        listing.ShouldBe(new ChargePointListingDto(
            "OHM-SLC-0042",
            3,
            "Salt Lake bus depot, public bays",
            "Enter from 400 South.",
            "Mo-Su 05:00-23:00"));
        server.Requests.ShouldBe(
            [(HttpMethod.Get, "/app/v1/charge-points/OHM-SLC-0042/listing", null)]);
    }

    [Fact]
    public async Task Given_a_charge_point_with_no_listing_then_none()
    {
        var server = new FakeServer().Answers(HttpStatusCode.NotFound);

        (await new ChargingApiClient(server.Client()).GetListingAsync("OHM-SLC-0099", Ct))
            .ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_server_error_then_the_client_throws()
    {
        var server = new FakeServer().Answers(HttpStatusCode.ServiceUnavailable);

        await Should.ThrowAsync<HttpRequestException>(
            () => new ChargingApiClient(server.Client()).GetListingAsync("OHM-SLC-0042", Ct));
    }
}
