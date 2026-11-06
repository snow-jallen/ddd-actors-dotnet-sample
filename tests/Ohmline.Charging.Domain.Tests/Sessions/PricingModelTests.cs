using System.Collections.Immutable;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Sessions;

/// <summary>
/// A feature flag that changes what a session decides (section 21.8): Decide reads it from its
/// context, the event records what it decided, and Evolve reads only the event. Replaying a
/// session gives the same state whatever the flag says on the day of the replay.
/// </summary>
public sealed class PricingModelTests
{
    private static readonly ConnectorId Connector = new(Charger, 1);

    private static readonly TariffSnapshot WithIdleFee = StandardTariff(0.50m) with
    {
        Components =
        [
            new EnergyRate(new Money(0.50m, Currency.Usd)),
            new IdleFee(new Money(0.40m, Currency.Usd), TimeSpan.FromMinutes(10)),
        ],
    };

    private static readonly StartSession Start = new(
        Command, Session, Depot, Connector, DriversCard, WithIdleFee, new Money(40m, Currency.Usd),
        1_200_000);

    [Fact]
    public void Given_the_dynamic_idle_fee_on_when_a_session_starts_then_its_event_says_so()
    {
        var flagOn = new DecisionContext(Morning)
        {
            Features = new FeatureFlags([ChargingFeatures.DynamicIdleFee]),
        };

        StartedBy(flagOn).PricingModel.ShouldBe(PricingModel.DynamicIdleFee);
    }

    [Fact]
    public void Given_no_flag_on_when_a_session_starts_then_its_event_says_standard()
    {
        StartedBy(new DecisionContext(Morning)).PricingModel.ShouldBe(PricingModel.Standard);
    }

    [Fact]
    public void Given_a_session_started_under_the_pilot_when_replayed_then_its_grace_is_halved()
    {
        var started = StartedBy(new DecisionContext(Morning)
        {
            Features = new FeatureFlags([ChargingFeatures.DynamicIdleFee]),
        });

        var idle = ChargingSession.Fold(
            [started, new ChargingSuspendedByEv(Session).At(Morning.AddMinutes(30))]);

        SessionPredicates.IdleFeeApplies(idle, Morning.AddMinutes(35)).ShouldBeTrue();
        SessionPredicates.IdleFeeApplies(idle, Morning.AddMinutes(34)).ShouldBeFalse();
    }

    [Fact]
    public void Given_a_standard_session_when_replayed_then_its_grace_is_the_tariff_s()
    {
        var started = StartedBy(new DecisionContext(Morning));

        var idle = ChargingSession.Fold(
            [started, new ChargingSuspendedByEv(Session).At(Morning.AddMinutes(30))]);

        SessionPredicates.IdleFeeApplies(idle, Morning.AddMinutes(35)).ShouldBeFalse();
        SessionPredicates.IdleFeeApplies(idle, Morning.AddMinutes(40)).ShouldBeTrue();
    }

    /// <summary>Every session started before section 21.8 was stored without a model.</summary>
    [Fact]
    public void Given_a_start_recorded_before_pricing_models_when_replayed_then_it_is_standard()
    {
        var before = StartedBy(new DecisionContext(Morning)) with { PricingModel = null };

        var state = ChargingSession.Fold([before]);

        state.Value.ShouldBeOfType<Active>().Terms.PricingModel.ShouldBe(PricingModel.Standard);
    }

    private static SessionStarted StartedBy(DecisionContext context) =>
        ChargingSession.Decide(SessionState.Initial, Start, context).Value
            .ShouldBeOfType<Ok<ImmutableArray<SessionEvent>>>()
            .Value.ShouldHaveSingleItem().ShouldBeOfType<SessionStarted>();
}
