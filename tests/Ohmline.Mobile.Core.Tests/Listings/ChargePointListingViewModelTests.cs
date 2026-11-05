using System.Net;
using Ohmline.Mobile.Core.Api;
using Ohmline.Mobile.Core.Listings;

namespace Ohmline.Mobile.Core.Tests.Listings;

public sealed class ChargePointListingViewModelTests
{
    private const string Listed = """
        {"chargePointId":"OHM-SLC-0042","revision":3,
         "displayName":"Salt Lake bus depot, public bays",
         "accessInstructions":"Enter from 400 South.","openingHours":"Mo-Su 05:00-23:00"}
        """;

    [Fact]
    public async Task Given_a_listed_charge_point_when_loaded_then_the_page_shows_its_name()
    {
        var screen = ScreenFor(new FakeServer().Answers(HttpStatusCode.OK, Listed));
        var changed = new List<string?>();
        screen.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await screen.LoadCommand.ExecuteAsync(null);

        screen.Listing.ShouldNotBeNull().DisplayName.ShouldBe("Salt Lake bus depot, public bays");
        screen.Message.ShouldBeNull();
        screen.IsLoading.ShouldBeFalse();
        changed.ShouldContain(nameof(screen.Listing));
        changed.Count(name => name == nameof(screen.IsLoading)).ShouldBe(2); // On, then off.
    }

    [Fact]
    public async Task Given_no_listing_when_loaded_then_the_driver_is_told_so()
    {
        var screen = ScreenFor(new FakeServer().Answers(HttpStatusCode.NotFound));

        await screen.LoadCommand.ExecuteAsync(null);

        screen.Listing.ShouldBeNull();
        screen.Message.ShouldBe("OHM-SLC-0042 is not listed yet.");
    }

    [Fact]
    public async Task Given_no_network_when_loaded_then_the_driver_is_told_in_the_apps_words()
    {
        var screen = ScreenFor(new FakeServer().IsUnreachable());

        await screen.LoadCommand.ExecuteAsync(null);

        screen.Message.ShouldBe("Ohmline cannot be reached. Check your connection and try again.");
        screen.IsLoading.ShouldBeFalse();
    }

    private static ChargePointListingViewModel ScreenFor(FakeServer server) =>
        new(new ChargingApiClient(server.Client())) { ChargePointId = "OHM-SLC-0042" };
}
