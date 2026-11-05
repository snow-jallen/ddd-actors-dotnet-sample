using Akka.Actor;
using Akka.Hosting;
using Akka.Serialization;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Actors.Serialization;
using Ohmline.SiteEnergy.Api.Requests;
using Ohmline.SiteEnergy.Api.Responses;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;

namespace Ohmline.SiteEnergy.Api.Tests;

/// <summary>A probe plays the site power budget region; every message is serialized.</summary>
public sealed class SiteEnergyEndpointsTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(SiteEnergy), output: output)
{
    private static readonly SiteId Depot = new("slc-depot");

    private static readonly ConnectorId Fleet1 = new(new ChargePointId("OHM-SLC-0042"), 1);

    private static readonly SessionId Bus =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001"));

    private static readonly IOptions<SiteEnergyApiOptions> Options =
        Microsoft.Extensions.Options.Options.Create(
            new SiteEnergyApiOptions { AskTimeout = TimeSpan.FromSeconds(3) });

    private static readonly SiteRequest Site = new(
        "slc-depot",
        200m,
        10m,
        "America/Denver",
        [
            new ConnectorRequest("OHM-SLC-0042/1", 5m, 50m, "fleet"),
            new ConnectorRequest("OHM-SLC-0042/2", 5m, 50m, "public"),
        ]);

    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 11, 4, 14, 40, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_site_then_its_budget_is_configured_with_every_connector()
    {
        var region = CreateTestProbe();

        var answer = SiteEnergyEndpoints.ConfigureAsync(
            Site, null, Budgets(region), _clock, Options, Ct);
        var configure = await region.ExpectMsgAsync<ConfigureSite>(cancellationToken: Ct);
        region.Reply(new BudgetCommandAccepted(1));

        configure.ShouldBe(new ConfigureSite(
            configure.CommandId,
            Depot,
            new Power(200m),
            new Power(10m),
            "America/Denver",
            [
                new ConnectorLimits(Fleet1, new Power(5m), new Power(50m), new Fleet()),
                new ConnectorLimits(
                    Fleet1 with { Number = 2 }, new Power(5m), new Power(50m), new Public()),
            ]));
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>()
            .Location.ShouldBe("/sites/slc-depot/budget");
    }

    [Fact]
    public async Task Given_a_priority_that_is_neither_fleet_nor_public_then_it_is_not_valid()
    {
        var region = CreateTestProbe();
        var vip = Site with
        {
            Connectors = [new ConnectorRequest("OHM-SLC-0042/1", 5m, 50m, "vip")],
        };

        var answer = await SiteEnergyEndpoints.ConfigureAsync(
            vip, null, Budgets(region), _clock, Options, Ct);

        var problem = answer.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>()
            .Errors["connectors[0].priority"]
            .ShouldBe(["Priority must be fleet or public, not 'vip'."]);
        await region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
    }

    [Fact]
    public async Task Given_a_configured_site_when_its_budget_is_read_then_200_in_kilowatts()
    {
        var region = CreateTestProbe();

        var answer = SiteEnergyEndpoints.GetBudgetAsync("slc-depot", Budgets(region), Options, Ct);
        await region.ExpectMsgAsync(new GetSitePowerBudget(Depot), cancellationToken: Ct);
        region.Reply(new SitePowerBudgetView(
            Depot,
            2,
            new Power(190m),
            Power.Zero,
            [new RequestView(Fleet1, Bus, new Power(50m), new Power(50m))]));

        var budget = (await answer).Result.ShouldBeOfType<Ok<BudgetResponse>>().Value;
        budget.ShouldNotBeNull().EffectiveCapacityKw.ShouldBe(190m);
        budget.Requests.ShouldBe([new RequestResponse("OHM-SLC-0042/1", Bus.Value, 50m, 50m)]);
    }

    [Fact]
    public async Task Given_a_site_never_configured_when_its_budget_is_read_then_404()
    {
        var region = CreateTestProbe();

        var answer = SiteEnergyEndpoints.GetBudgetAsync("slc-depot", Budgets(region), Options, Ct);
        await region.ExpectMsgAsync<GetSitePowerBudget>(cancellationToken: Ct);
        region.Reply(new SitePowerBudgetView(Depot, 0, Power.Zero, Power.Zero, []));

        (await answer).Result.ShouldBeOfType<ProblemHttpResult>().StatusCode
            .ShouldBe(StatusCodes.Status404NotFound);
    }

    private static IRequiredActor<SitePowerBudgetRegion> Budgets(TestProbe probe) =>
        new Region(probe.Ref);

    private static SerializerDetails SiteEnergy(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            SiteEnergySerializer.Name,
            new SiteEnergySerializer(system),
            [.. SiteEnergySerializer.BoundTypes]);

    private sealed class Region(IActorRef actor) : IRequiredActor<SitePowerBudgetRegion>
    {
        public IActorRef ActorRef => actor;

        public Task<IActorRef> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(actor);
    }
}
