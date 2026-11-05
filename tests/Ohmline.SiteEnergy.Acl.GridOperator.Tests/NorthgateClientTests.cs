using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ohmline.SiteEnergy.Acl.GridOperator.Northgate;
using static Ohmline.SiteEnergy.Acl.GridOperator.Tests.Northgate;

namespace Ohmline.SiteEnergy.Acl.GridOperator.Tests;

/// <summary>Ohmline's client against the Northgate stand-in: Northgate's shape, as it is.</summary>
public sealed class NorthgateClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_an_announced_event_then_the_feed_reads_it_and_the_depot_is_curtailed()
    {
        await using var northgate = new WebApplicationFactory<Program>();
        var http = northgate.CreateClient();
        var announce = new
        {
            eventId = "ng-dr-0007",
            eventStatus = "active",
            venIds = new[] { "NG-VEN-0042" },
            reductionKw = 60m,
            start = FivePm,
            duration = "02:00:00",
        };
        (await http.PostAsJsonAsync("/signals", announce, Ct)).EnsureSuccessStatusCode();

        var signals = await new NorthgateClient(http).SinceAsync(0, Ct);

        var revision = signals.Events.ShouldHaveSingleItem();
        signals.Latest.ShouldBe(1);
        revision.Signals.ShouldHaveSingleItem().Intervals.ShouldHaveSingleItem()
            .ShouldBe(new Interval(FivePm, "PT2H", -60m));
        GridSignalTranslator.Desired(signals.Events, Sites, FivePm.AddMinutes(1))[Depot].Value
            .ShouldBe(new Curtail(Depot, new(60m), "ng-dr-0007", 0));
        (await new NorthgateClient(http).SinceAsync(1, Ct)).Events.ShouldBeEmpty();
    }
}
