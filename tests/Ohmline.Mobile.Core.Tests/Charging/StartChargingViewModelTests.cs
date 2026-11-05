using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Requests;
using Ohmline.Mobile.Core.Api;
using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Core.Tests.Charging;

public sealed class StartChargingViewModelTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    private static readonly DriverToken Token = new("04A2B3C4D5E6F7");

    [Fact]
    public async Task Given_input_that_is_not_valid_then_each_field_says_why_and_nothing_is_sent()
    {
        var server = new FakeServer();
        var screen = ScreenFor(server);
        screen.ConnectorText = "OHM-SLC-0042";
        screen.SpendCapText = "0";

        await screen.StartChargingCommand.ExecuteAsync(null);

        screen.ConnectorError.ShouldBe(
            "'OHM-SLC-0042' is not a connector code. It looks like OHM-SLC-0042/1.");
        screen.SpendCapError.ShouldBe("A spend cap is from 1 to 500 USD, not 0.");
        screen.Status.ShouldBeNull();
        server.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_valid_tap_then_a_start_with_ids_made_at_the_tap_is_accepted()
    {
        var server = new FakeServer().Answers(HttpStatusCode.Accepted, """
            {"commandId":"019a4f6e-1c40-7c11-9a2b-3c4d00000c01","version":1}
            """);
        var screen = ScreenFor(server);
        screen.ConnectorText = "ohm-slc-0042/1";

        await screen.StartChargingCommand.ExecuteAsync(null);

        screen.Status.ShouldNotBeNull().Value.ShouldBe(new Accepted(1));
        screen.Screen.Value.ShouldBeOfType<Ready>();
        var (method, path, body) = server.Requests.ShouldHaveSingleItem();
        (method, path).ShouldBe((HttpMethod.Post, "/app/v1/journeys"));
        var sent = JsonSerializer.Deserialize(
            body!, ChargingClientJsonContext.Default.StartChargingRequest)!;
        sent.ShouldBe(sent with
        {
            ConnectorId = "OHM-SLC-0042/1",
            TokenUid = "04A2B3C4D5E6F7",
            SpendCapAmount = 40m,
            Currency = "USD",
        });
        sent.CommandId.Version.ShouldBe(7);
        sent.JourneyId.Version.ShouldBe(7);
        sent.JourneyId.ShouldNotBe(sent.CommandId);
    }

    [Fact]
    public async Task Given_a_reserved_connector_then_the_rejection_is_a_sentence_not_409()
    {
        var server = new FakeServer().Answers(HttpStatusCode.Conflict, """
            {"title":"The command was rejected.","status":409,
             "code":"reserved-for-another-token","detail":"Connector 1 is reserved."}
            """);
        var screen = ScreenFor(server);
        screen.ConnectorText = "OHM-SLC-0042/1";

        await screen.StartChargingCommand.ExecuteAsync(null);

        screen.Status.ShouldNotBeNull().Value.ShouldBe(new Rejected(
            RejectionCodes.ReservedForAnotherToken,
            "This connector is reserved for another driver."));
    }

    [Fact]
    public async Task Given_no_connection_then_the_screen_is_offline()
    {
        var screen = ScreenFor(new FakeServer().IsUnreachable(), new FakeConnectivity(false));
        screen.ConnectorText = "OHM-SLC-0042/1";

        await screen.StartChargingCommand.ExecuteAsync(null);

        screen.Screen.Value.ShouldBeOfType<Offline>();
        screen.Status.ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_connection_but_no_answer_then_the_screen_says_so()
    {
        var screen = ScreenFor(new FakeServer().Answers(HttpStatusCode.GatewayTimeout));
        screen.ConnectorText = "OHM-SLC-0042/1";

        await screen.StartChargingCommand.ExecuteAsync(null);

        screen.Screen.Value.ShouldBe(new Failed("Ohmline did not answer. Try again in a moment."));
    }

    [Fact]
    public async Task While_the_start_is_being_sent_then_the_screen_is_loading()
    {
        var server = new FakeServer().Answers(HttpStatusCode.Accepted, """
            {"commandId":"019a4f6e-1c40-7c11-9a2b-3c4d00000c01","version":1}
            """);
        var screen = ScreenFor(server);
        screen.ConnectorText = "OHM-SLC-0042/1";
        var seen = new List<object?>();
        screen.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(screen.Screen))
            {
                seen.Add(screen.Screen.Value);
            }
        };

        await screen.StartChargingCommand.ExecuteAsync(null);

        seen.ShouldBe([new Loading(), new Ready()]);
    }

    private static StartChargingViewModel ScreenFor(
        FakeServer server, FakeConnectivity? connectivity = null) =>
        new(
            new ChargingApiClient(server.Client()),
            Token,
            connectivity ?? new FakeConnectivity(),
            new FakeTimeProvider(Morning));
}
