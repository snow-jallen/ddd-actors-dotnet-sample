using System.Net;
using System.Net.Http.Json;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.IntegrationTests;

/// <summary>
/// The query endpoints at the end of the pipeline (section 13.8), on the node as it runs: they
/// answer from the read models, which two contexts' events keep up to date.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class QueryApiTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_car_plugged_in_and_given_power_then_the_live_board_shows_both()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var (site, chargePoint, session) = (NextSite(), Chargers.Next(), Guid.CreateVersion7());
        (await http.PostAsJsonAsync("/charge-points", Registration(chargePoint, site), Ct))
            .EnsureSuccessStatusCode();
        await Chargers.PlugInAsync(node, chargePoint, session, "04A2B3C4D5E6F7");
        (await http.PostAsJsonAsync("/sites", Site(site, chargePoint), Ct))
            .EnsureSuccessStatusCode();
        await RequestPowerAsync(node, site, new ConnectorId(new(chargePoint), 1), session);

        var board = await EventuallyAsync<Board>(
            http,
            $"/sites/{site}/live-board",
            board => board.Connectors is [{ AllocatedKw: not null, Status: "occupied" }, _]);

        board.SiteId.ShouldBe(site);
        board.Connectors.ShouldBe(
        [
            new Connector($"{chargePoint}/1", "occupied", session, 22m),
            new Connector($"{chargePoint}/2", "available", null, null),
        ]);
    }

    [Fact]
    public async Task Given_two_sessions_of_one_token_then_its_sessions_come_newest_first()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()[..7]);
        var (first, second) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        (await http.PostAsJsonAsync("/tariffs", Sessions.Tariff, Ct)).EnsureSuccessStatusCode();
        foreach (var session in new[] { first, second })
        {
            (await http.PostAsJsonAsync("/sessions", Sessions.Start(session, tokenUid: token), Ct))
                .EnsureSuccessStatusCode();
        }

        var sessions = await EventuallyAsync<List<TokenSession>>(
            http, $"/tokens/{token.ToLowerInvariant()}/sessions", sessions => sessions.Count == 2);

        sessions.Select(row => row.SessionId).ShouldBe([second, first]);
        sessions.ShouldAllBe(row => row.Status == "charging" && row.Version == 1);
    }

    [Fact]
    public async Task Given_a_site_with_no_connectors_on_the_board_then_404()
    {
        await using var node = new OhmlineNode(postgres);

        var response = await node.CreateClient().GetAsync("/sites/slc-nowhere/live-board", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static string NextSite() => $"slc-itg-{Random.Shared.Next(100_000, 1_000_000)}";

    private static object Registration(string chargePointId, string siteId) =>
        new { chargePointId, siteId, connectorMaxPowersKw = new[] { 22m, 22m } };

    private static object Site(string siteId, string chargePoint) => new
    {
        siteId,
        gridLimitKw = 100m,
        safetyMarginKw = 10m,
        timeZoneId = "America/Denver",
        connectors = new[] { 1, 2 }.Select(number => new
        {
            connectorId = $"{chargePoint}/{number}",
            minPowerKw = 5m,
            maxPowerKw = 22m,
            priority = "public",
        }),
    };

    /// <summary>
    /// What the charging journey will do from Chapter 15; no endpoint does it yet, so the test
    /// asks the site's power budget itself, through the node's actor registry.
    /// </summary>
    private static async Task RequestPowerAsync(
        OhmlineNode node, string site, ConnectorId connector, Guid session)
    {
        var budgets = await node.Services.GetRequiredService<ActorRegistry>()
            .GetAsync<SitePowerBudgetRegion>(Ct);
        var command = new RequestAllocation(
            new CommandId(Guid.CreateVersion7()),
            new SiteId(site),
            connector,
            new SessionId(session),
            new Power(22m));
        await budgets.Ask<object>(command, TimeSpan.FromSeconds(5), Ct);
    }

    private static async Task<T> EventuallyAsync<T>(
        HttpClient http, string path, Func<T, bool> done)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var response = await http.GetAsync(path, Ct);
            if (response.IsSuccessStatusCode
                && await response.Content.ReadFromJsonAsync<T>(Ct) is { } body
                && done(body))
            {
                return body;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"GET {path} never showed what the test waits for.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }

    private sealed record Board(string SiteId, List<Connector> Connectors);

    private sealed record Connector(
        string ConnectorId, string Status, Guid? SessionId, decimal? AllocatedKw);

    private sealed record TokenSession(
        Guid SessionId,
        string ConnectorId,
        string Status,
        decimal EnergyKwh,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        long Version);
}
