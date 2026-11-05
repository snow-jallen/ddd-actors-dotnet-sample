using System.Collections.Immutable;
using Ohmline.Charging.Domain.Policies;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Policies;

/// <summary>
/// Each lilac sticky of the session, as a pure reaction: an event in, Charging's intents out.
/// </summary>
public sealed class PolicyTests
{
    private static readonly ConnectorId Connector = new(Charger, 1);
    private static readonly IdleFee Idle = new(Usd(0.40m), TimeSpan.FromMinutes(15));
    private static readonly DateTimeOffset Full = Morning.AddMinutes(48);
    private static readonly DateTimeOffset Unplugged = Morning.AddMinutes(73);

    private static readonly SessionTerms Terms = new(
        Session,
        Depot,
        Connector,
        DriversCard,
        StandardTariff(0.42m) with { Components = [new EnergyRate(Usd(0.42m)), Idle] },
        Usd(40m));

    /// <summary>The primer's charge: started, full at 08:40, unplugged at 09:05.</summary>
    private static readonly ImmutableArray<SessionEvent> PrimersCharge =
    [
        new SessionStarted(Session, Depot, Connector, DriversCard, Terms.Tariff, Usd(40m), 0)
            .At(Morning),
        new MeterValueRecorded(Session, 27_600, Full).At(Full),
        new ChargingSuspended(Session).At(Full),
        new SessionEnded(Session, Depot, Connector, DriversCard, new Energy(27.6m)).At(Unplugged),
    ];

    [Fact]
    public void Whenever_a_session_ends_then_its_allocation_is_released_and_its_token_unlocked()
    {
        new ReleaseOnSessionEnded().React(PrimersCharge[^1]).ShouldBe(
        [
            new ChargingIntent.ReleaseSiteAllocation(Depot, Connector, Session),
            new ChargingIntent.UnlockToken(DriversCard, Session),
        ]);
    }

    [Fact]
    public void Whenever_the_spend_cap_is_reached_then_charging_stops()
    {
        var reached = new SpendCapReached(Session, Usd(40m), Usd(40m)).At(Full);

        new SpendCapPolicy().React(reached).ShouldBe([new ChargingIntent.StopCharging(Session)]);
    }

    [Fact]
    public void Whenever_charging_is_suspended_then_the_driver_learns_when_the_idle_fee_starts()
    {
        new IdleFeePolicy(Idle).React(PrimersCharge[2]).ShouldBe(
            [new ChargingIntent.WarnOfIdleFee(Session, Full.AddMinutes(15), Usd(0.40m))]);
    }

    [Fact]
    public void Given_an_event_a_policy_does_not_watch_then_it_has_no_intents()
    {
        ISessionPolicy[] policies =
            [new ReleaseOnSessionEnded(), new SpendCapPolicy(), new IdleFeePolicy(Idle)];

        policies.ShouldAllBe(policy => policy.React(PrimersCharge[1]).IsEmpty);
    }

    [Fact]
    public void Given_the_policies_of_a_session_then_its_history_causes_the_intents_in_order()
    {
        var policies = SessionPolicies.For(Terms);

        PrimersCharge.SelectMany(@event => policies.React(@event)).ShouldBe(
        [
            new ChargingIntent.WarnOfIdleFee(Session, Full.AddMinutes(15), Usd(0.40m)),
            new ChargingIntent.ReleaseSiteAllocation(Depot, Connector, Session),
            new ChargingIntent.UnlockToken(DriversCard, Session),
        ]);
    }

    [Fact]
    public void Given_a_tariff_without_an_idle_fee_then_nobody_is_warned()
    {
        var policies = SessionPolicies.For(Terms with { Tariff = StandardTariff(0.42m) });

        policies.React(PrimersCharge[2]).ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_policy_then_its_intents_are_charging_words_not_other_contexts_commands()
    {
        var intents = typeof(ChargingIntent).GetNestedTypes();

        intents.ShouldNotBeEmpty();
        intents.ShouldAllBe(intent => intent.Assembly == typeof(ChargingIntent).Assembly);
        typeof(ChargingIntent).Assembly.GetReferencedAssemblies()
            .ShouldNotContain(reference => reference.Name!.StartsWith("Ohmline.SiteEnergy")
                || reference.Name.StartsWith("Ohmline.DriverAccounts"));
    }

    private static Money Usd(decimal amount) => new(amount, Currency.Usd);
}
