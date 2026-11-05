using System.Net;
using System.Net.Http.Json;

namespace Ohmline.IntegrationTests;

/// <summary>
/// A charging journey on the node as it runs (section 15.4): over HTTP, through every context's
/// shard region, the Development ports, the relay that brings the session's events to the
/// journey, and the subscriber that unlocks the token when the session ends.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class JourneyTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_charger_a_site_and_a_token_then_a_journey_runs_to_a_settled_payment()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var (site, connector, token) = await SetUpAsync(http);

        var started = await http.PostAsJsonAsync(
            "/journeys", new { tokenUid = token, connectorId = connector }, Ct);
        started.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var journey = started.Headers.Location!.ToString();
        await StepAsync(http, journey, "awaiting-energy-flow");

        // The charger reports the car drawing energy; in Development, the edge plays it.
        var session = journey["/journeys/".Length..];
        (await http.PostAsJsonAsync("/sessions", Session(session, site, connector, token), Ct))
            .EnsureSuccessStatusCode();
        await StepAsync(http, journey, "charging");
        (await http.PostAsJsonAsync(
                $"/sessions/{session}/meter-values", Sessions.Reading(1_210_000), Ct))
            .EnsureSuccessStatusCode();
        (await http.PostAsync($"/sessions/{session}/stop", content: null, Ct))
            .EnsureSuccessStatusCode();

        var completed = await StepAsync(http, journey, "completed");
        completed.SessionId.ShouldBe(Guid.Parse(session));
        completed.Version.ShouldBe(7);
    }

    /// <summary>A charger, a site and a token of the test's own: the database is shared.</summary>
    internal static async Task<(string Site, string Connector, string Token)> SetUpAsync(
        HttpClient http)
    {
        var chargePoint = Chargers.Next();
        var site = $"itg-{chargePoint[^4..]}";
        var connector = $"{chargePoint}/1";
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()[..7]);
        var registration = new
        {
            chargePointId = chargePoint, siteId = site, connectorMaxPowersKw = new[] { 22m },
        };
        (await http.PostAsJsonAsync("/charge-points", registration, Ct)).EnsureSuccessStatusCode();
        var siteRequest = new
        {
            siteId = site,
            gridLimitKw = 200m,
            safetyMarginKw = 10m,
            timeZoneId = "America/Denver",
            connectors = new[]
            {
                new
                {
                    connectorId = connector, minPowerKw = 5m, maxPowerKw = 22m, priority = "public",
                },
            },
        };
        (await http.PostAsJsonAsync("/sites", siteRequest, Ct)).EnsureSuccessStatusCode();
        var card = new { tokenUid = token, driver = "drv-itg0001" };
        (await http.PostAsJsonAsync("/tokens", card, Ct)).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        return (site, connector, token);
    }

    private static object Session(
        string sessionId, string site, string connector, string token) => new
    {
        sessionId,
        siteId = site,
        connectorId = connector,
        tokenUid = token,
        tariffId = "slc-standard",
        spendCap = 40m,
        meterStartWh = 1_200_000L,
    };

    internal static async Task<Journey> StepAsync(HttpClient http, string journey, string step)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var current = await http.GetFromJsonAsync<Journey>(journey, Ct);
            if (current?.Step == step)
            {
                return current;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"{journey} is at {current?.Step}, not {step}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }

    internal sealed record Journey(
        Guid JourneyId,
        long Version,
        string Step,
        Guid? SessionId,
        string? AbandonReason,
        string? Detail);
}
