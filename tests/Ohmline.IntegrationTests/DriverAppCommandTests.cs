using System.Net;
using System.Net.Http.Json;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Client.Contracts.Replies;
using Ohmline.Charging.Client.Contracts.Requests;

namespace Ohmline.IntegrationTests;

/// <summary>
/// The driver app's published language on the node as it runs (section 19.2): requests written
/// and answers read with <see cref="ChargingClientJsonContext"/>, as the app does, so the test
/// knows nothing of Charging but what the app knows.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class DriverAppCommandTests(PostgresFixture postgres)
{
    private static readonly ChargingClientJsonContext Json = ChargingClientJsonContext.Default;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_start_from_the_app_then_202_names_its_command_and_the_journey_runs()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var (_, connector, token) = await JourneyTests.SetUpAsync(http);
        var request = new StartChargingRequest(
            Guid.CreateVersion7(), Guid.CreateVersion7(), connector, token, 40m, "USD");

        var response = await http.PostAsJsonAsync(
            "/app/v1/journeys", request, Json.StartChargingRequest, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.Content.ReadFromJsonAsync(Json.CommandAcceptedDto, Ct))
            .ShouldBe(new CommandAcceptedDto(request.CommandId, 1));
        var journey = await JourneyTests.StepAsync(
            http, $"/journeys/{request.JourneyId}", "awaiting-energy-flow");
        journey.JourneyId.ShouldBe(request.JourneyId);
    }

    [Fact]
    public async Task Given_a_connector_reserved_for_another_driver_then_409_with_its_code()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var chargePoint = Chargers.Next();
        await http.PostAsJsonAsync("/charge-points", Chargers.Registration(chargePoint), Ct);
        var route = $"/app/v1/charge-points/{chargePoint}/connectors/1/reservations";
        await http.PostAsJsonAsync(
            route, new ReserveConnectorRequest(Guid.CreateVersion7(), "04FFEEDDCC"),
            Json.ReserveConnectorRequest, Ct);

        var response = await http.PostAsJsonAsync(
            route, new ReserveConnectorRequest(Guid.CreateVersion7(), "04A2B3C4D5E6F7"),
            Json.ReserveConnectorRequest, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync(Json.ProblemDto, Ct)).ShouldNotBeNull()
            .Code.ShouldBe(RejectionCodes.ReservedForAnotherToken);
    }

    [Fact]
    public async Task Given_a_listed_charge_point_then_the_app_reads_its_listing()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var chargePoint = Chargers.Next();
        await http.PutAsJsonAsync(
            $"/charge-points/{chargePoint}/listing",
            new
            {
                displayName = "Salt Lake bus depot, public bays",
                accessInstructions = "Enter from 400 South.",
                openingHours = "Mo-Su 05:00-23:00",
            },
            Ct);

        var listing = await http.GetFromJsonAsync(
            $"/app/v1/charge-points/{chargePoint}/listing", Json.ChargePointListingDto, Ct);

        listing.ShouldBe(new ChargePointListingDto(
            chargePoint,
            1,
            "Salt Lake bus depot, public bays",
            "Enter from 400 South.",
            "Mo-Su 05:00-23:00"));
    }
}
