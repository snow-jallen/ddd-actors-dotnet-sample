using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.TestKit;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;

namespace Ohmline.Node.Tests;

/// <summary>
/// Every bounded context's aggregates, reached as the edge reaches them: by a message that
/// names the aggregate, sent to its region or, for the tariff book, to the singleton's proxy.
/// </summary>
public sealed class RegionTests(ITestOutputHelper output) : ShardedNode(output)
{
    private static readonly ChargePointId Charger = new("OHM-SLC-0042");

    private static readonly SiteId Depot = new("slc-depot");

    private static readonly TokenUid Card = new("04A2B3C4D5E6F7");

    private static readonly DriverRef Driver = new("drv-7f3a91c2");

    private static readonly SessionId AtTheDepot =
        new(Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001"));

    private static readonly SessionId ElsewhereAMinuteLater =
        new(Guid.Parse("019a4f6f-0680-7a51-8e2f-3b1d00000002"));

    private static readonly TariffId Standard = new("slc-standard");

    private CommandId NewCommand => CommandId.New(Clock);

    [Fact]
    public async Task Given_a_charge_point_then_its_region_starts_it_and_it_decides()
    {
        (await AskAsync<ChargePointRegion, CommandReply>(Register()))
            .ShouldBe(new CommandAccepted(1));
        (await AskAsync<ChargePointRegion, CommandReply>(
                new ReserveConnector(NewCommand, Charger, 1, Card)))
            .ShouldBe(new CommandAccepted(2));

        var status = await AskAsync<ChargePointRegion, ChargePointStatus>(
            new GetChargePointStatus(Charger));
        status.Connectors[0].ReservedUntil.ShouldBe(Morning.AddMinutes(15)); // The fake clock.
    }

    [Fact]
    public async Task Given_the_journeys_region_then_it_remembers_its_journeys_in_memory()
    {
        (await ActorRegistry.For(Sys).GetAsync<ChargingJourneyRegion>(Ct))
            .ShouldNotBe(ActorRefs.Nobody);

        Sys.Settings.Config
            .GetStringList("akka.cluster.sharding.distributed-data.durable.keys")
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_site_then_its_budget_region_configures_it()
    {
        (await AskAsync<SitePowerBudgetRegion, BudgetCommandReply>(ConfigureDepot()))
            .ShouldBe(new BudgetCommandAccepted(1));
        (await AskAsync<SitePowerBudgetRegion, SitePowerBudgetView>(new GetSitePowerBudget(Depot)))
            .EffectiveCapacity.ShouldBe(new Power(190m));
    }

    [Fact]
    public async Task Given_a_token_and_a_site_then_a_journey_gets_as_far_as_the_charger()
    {
        (await AskAsync<IdTokenRegion, TokenCommandReply>(
                new RegisterToken(NewCommand, Card, Driver)))
            .ShouldBe(new TokenCommandAccepted(1));
        (await AskAsync<SitePowerBudgetRegion, BudgetCommandReply>(ConfigureDepot()))
            .ShouldBe(new BudgetCommandAccepted(1));
        var journey = new JourneyId(Guid.CreateVersion7(Morning));
        var start = new StartCharging(
            NewCommand,
            journey,
            Card,
            new ConnectorId(Charger, 1),
            Depot,
            new Power(22m),
            new Money(50m, Currency.Usd));

        (await AskAsync<ChargingJourneyRegion, CommandReply>(start))
            .ShouldBe(new CommandAccepted(1));

        // Through Driver Accounts, the node's payment port and Site Energy, to the charger port.
        await AwaitAssertAsync(
            async () =>
            {
                var view = await AskAsync<ChargingJourneyRegion, JourneyView>(
                    new GetJourney(journey), TimeSpan.FromMilliseconds(500));
                view.Step.ShouldBe(JourneyView.AwaitingEnergyFlow);
                view.Version.ShouldBe(4);
            },
            Patience,
            cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_token_when_two_sessions_ask_for_it_then_only_the_first_gets_it()
    {
        (await AskAsync<IdTokenRegion, TokenCommandReply>(
                new RegisterToken(NewCommand, Card, Driver)))
            .ShouldBe(new TokenCommandAccepted(1));

        var first = await AskAsync<IdTokenRegion, TokenCommandReply>(
            new LockForSession(NewCommand, Card, AtTheDepot));
        var second = await AskAsync<IdTokenRegion, TokenCommandReply>(
            new LockForSession(NewCommand, Card, ElsewhereAMinuteLater));

        first.ShouldBe(new TokenLocked(Card, AtTheDepot, Driver, Version: 2));
        second.ShouldBeOfType<TokenLockRefused>().Code.ShouldBe("locked-by-another-session");
    }

    [Fact]
    public async Task Given_a_published_tariff_then_a_session_copies_it_from_the_singleton()
    {
        var publish = new PublishTariff(
            NewCommand,
            Standard,
            Currency.Usd,
            [new EnergyRate(new Money(0.42m, Currency.Usd))],
            PriceCap: null,
            RoundingPolicy.Cents,
            "America/Denver");
        (await AskAsync<TariffBookProxy, TariffBookReply>(publish))
            .ShouldBe(new TariffBookAccepted(1));

        var current = await AskAsync<TariffBookProxy, CurrentTariff>(
            new GetTariffSnapshot(Standard));
        var start = new StartSession(
            NewCommand,
            AtTheDepot,
            Depot,
            new ConnectorId(Charger, 1),
            Card,
            current.Snapshot.ShouldNotBeNull(),
            new Money(40m, Currency.Usd),
            MeterStartWh: 1_200_000);

        current.Snapshot.Version.ShouldBe(1);
        (await AskAsync<ChargingSessionRegion, CommandReply>(start))
            .ShouldBe(new CommandAccepted(1));
    }

    [Fact]
    public async Task Given_a_failed_write_then_the_next_message_starts_the_entity_again()
    {
        (await AskAsync<ChargePointRegion, CommandReply>(Register()))
            .ShouldBe(new CommandAccepted(1));
        var region = await ActorRegistry.GetAsync<ChargePointRegion>(Ct);
        var errors = CreateTestProbe();
        Sys.EventStream.Subscribe(errors.Ref, typeof(Error));

        await WithJournalWrite(write => write.Fail(), async () =>
        {
            region.Tell(new ReserveConnector(NewCommand, Charger, 1, Card), TestActor);
            await errors.FishForMessageAsync<Error>(
                error => $"{error.Message}".StartsWith("Failed to persist ConnectorReserved"),
                cancellationToken: Ct);
        });

        // No supervisor: the shard saw the entity stop, and the next message starts a new one,
        // which recovers what was stored.
        await AwaitAssertAsync(
            async () =>
            {
                var status = await AskAsync<ChargePointRegion, ChargePointStatus>(
                    new GetChargePointStatus(Charger), TimeSpan.FromMilliseconds(500));
                status.Version.ShouldBe(1);
                status.Connectors[0].State.ShouldBe(ConnectorStatus.Available);
            },
            Patience,
            cancellationToken: Ct);
        await ExpectNoMsgAsync(TimeSpan.Zero, Ct); // And the reservation was never answered.
    }

    private RegisterChargePoint Register() =>
        new(NewCommand, Charger, Depot, [new Power(22m), new Power(22m)]);

    private ConfigureSite ConfigureDepot() => new(
        NewCommand,
        Depot,
        new Power(200m),
        new Power(10m),
        "America/Denver",
        [
            new ConnectorLimits(
                new ConnectorId(Charger, 1), new Power(5m), new Power(50m), new Fleet()),
        ]);
}
