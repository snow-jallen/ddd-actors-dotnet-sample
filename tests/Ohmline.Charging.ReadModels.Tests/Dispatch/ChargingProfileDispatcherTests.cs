using System.Collections.Concurrent;
using Akka.Persistence.Query;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Contracts.Chargers;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.ReadModels.Dispatch;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Domain.Budgets;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Dispatch;

/// <summary>
/// The dispatcher between Site Energy's allocation events and the chargers: a profile for each
/// connector given a limit, carrying the allocation's version.
/// </summary>
public sealed class ChargingProfileDispatcherTests
{
    private static readonly ConnectorId First = new(Charger, 1);
    private static readonly ConnectorId Second = new(Charger, 2);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Given_a_rebalance_then_every_connector_gets_its_new_limit_and_one_that_waits_zero()
    {
        var rebalanced = new AllocationsRebalanced(
            Depot,
            [new PowerAllocation(First, new Power(11m)), new PowerAllocation(Second, Power.Zero)]);

        ChargingProfileDispatcher.ProfilesFor(rebalanced, version: 9).ShouldBe(
        [
            new SetChargingProfile(First, new Power(11m), 9),
            new SetChargingProfile(Second, Power.Zero, 9),
        ]);
    }

    [Fact]
    public void Given_a_released_allocation_then_no_charger_is_told_anything()
    {
        ChargingProfileDispatcher.ProfilesFor(
                new AllocationReleased(Depot, First, Session), version: 10)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_grant_then_the_charger_port_sends_the_profile_with_its_version()
    {
        var chargers = new Chargers();
        var granted = new AllocationGranted(Depot, First, Session, new Power(22m), new Power(11m));
        var envelope = new EventEnvelope(
            Offset.Sequence(3), $"site-power-budget-{Depot}", 4, granted, 0L, ["site-energy"]);

        await new ChargingProfileDispatcher(chargers).HandleAsync(envelope, null!, Ct);

        chargers.Profiles.ShouldBe([(First, new Power(11m), 4L)]);
    }

    private sealed class Chargers : IChargerCommandPort
    {
        public ConcurrentQueue<(ConnectorId, Power, long)> Profiles { get; } = new();

        public Task<ChargerCommandResult> SetChargingProfileAsync(
            ConnectorId connectorId, Power limit, long version, CancellationToken token)
        {
            Profiles.Enqueue((connectorId, limit, version));
            return Task.FromResult<ChargerCommandResult>(new ChargerAccepted(connectorId));
        }

        public Task<ChargerCommandResult> RemoteStartAsync(
            IdempotencyKey key,
            ConnectorId connectorId,
            TokenUid tokenUid,
            SessionId sessionId,
            Power limit,
            CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ChargerCommandResult> RemoteStopAsync(
            ConnectorId connectorId, SessionId sessionId, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
