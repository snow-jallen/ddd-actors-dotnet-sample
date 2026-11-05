using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Specifications;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Sessions;

/// <summary>
/// The session's rules, as patterns and as specification values. Both forms give the same
/// answers, because the values wrap the patterns.
/// </summary>
public sealed class SpecificationTests
{
    private static readonly ConnectorId Connector = new(Charger, 1);
    private static readonly Money Cap = new(40m, Currency.Usd);

    private static readonly TariffSnapshot WithIdleFee = StandardTariff(0.42m) with
    {
        Components =
        [
            new EnergyRate(Usd(0.42m)),
            new IdleFee(Usd(0.40m), TimeSpan.FromMinutes(15)),
        ],
    };

    private static readonly DateTimeOffset Full = Morning.AddMinutes(48);

    [Theory]
    [InlineData("suspended", 14, false)]
    [InlineData("suspended", 15, true)]
    [InlineData("suspended", 25, true)]
    [InlineData("active", 25, false)]
    [InlineData("ended", 25, false)]
    public void Given_a_session_then_the_idle_fee_applies_once_the_grace_period_is_over(
        string state, int minutesAfterSuspension, bool applies)
    {
        var session = Session(state, WithIdleFee);
        var at = Full.AddMinutes(minutesAfterSuspension);

        SessionPredicates.IdleFeeApplies(session, at).ShouldBe(applies);
        new IdleFeeApplies(at).IsSatisfiedBy(session).ShouldBe(applies);
    }

    [Fact]
    public void Given_a_tariff_without_an_idle_fee_then_it_never_applies()
    {
        var session = Session("suspended", StandardTariff(0.42m));

        SessionPredicates.IdleFeeApplies(session, Full.AddHours(3)).ShouldBeFalse();
    }

    [Fact]
    public void Given_sessions_at_the_depot_then_a_router_finds_those_whose_idle_fee_starts_soon()
    {
        var now = Full.AddMinutes(12);
        ISpecification<SessionState> startsWithinFiveMinutes =
            new IdleFeeApplies(now.AddMinutes(5)).And(new IdleFeeApplies(now).Not());
        SessionState[] depot =
        [
            Session("active", WithIdleFee),
            Session("suspended", WithIdleFee),
            Session("suspended", WithIdleFee, suspendedAt: Full.AddMinutes(-10)),
            Session("suspended", WithIdleFee, suspendedAt: Full.AddMinutes(10)),
        ];

        depot.Where(startsWithinFiveMinutes.IsSatisfiedBy).ShouldBe([depot[1]]);
    }

    [Theory]
    [InlineData("standard", true)]
    [InlineData("idle fee only", true)]
    [InlineData("free", false)]
    [InlineData("nothing", false)]
    public void Given_a_tariff_then_a_hold_is_needed_unless_it_charges_nothing(
        string tariff, bool needsHold)
    {
        TariffComponent[] components = tariff switch
        {
            "standard" => [new EnergyRate(Usd(0.42m)), new SessionFee(Usd(1.00m))],
            "idle fee only" => [new IdleFee(Usd(0.40m), TimeSpan.FromMinutes(15))],
            "free" => [new EnergyRate(Usd(0m)), new SessionFee(Usd(0m))],
            "nothing" => [],
            _ => throw new ArgumentOutOfRangeException(nameof(tariff)),
        };
        var start = new StartSession(
            Command, Known.Session, Depot, Connector, DriversCard,
            StandardTariff(0.42m) with { Components = [.. components] }, Cap, 1_200_000);

        SessionPredicates.RequiresPreAuthorization(start).ShouldBe(needsHold);
        new RequiresPreAuthorization().IsSatisfiedBy(start).ShouldBe(needsHold);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 0, true)]
    [InlineData(3, 12.5, false)]
    public void Given_a_history_then_a_list_pattern_sees_whether_it_ended_with_nothing_delivered(
        int readings, decimal delivered, bool nothing)
    {
        SessionEvent[] history =
        [
            Started(StandardTariff(0.42m)),
            .. Enumerable.Range(1, readings).Select(minute => new MeterValueRecorded(
                Known.Session, 1_200_000, Morning.AddMinutes(minute)).At(Morning)),
            new SessionEnded(Known.Session, Depot, Connector, DriversCard, new(delivered))
                .At(Morning.AddMinutes(readings + 1)),
        ];

        SessionPredicates.EndedWithoutEnergy(history).ShouldBe(nothing);
        SessionPredicates.EndedWithoutEnergy(history[..^1]).ShouldBeFalse();
    }

    private static SessionState Session(
        string state, TariffSnapshot tariff, DateTimeOffset? suspendedAt = null) =>
        ChargingSession.Fold(state switch
        {
            "active" => [Started(tariff)],
            "suspended" =>
            [
                Started(tariff),
                new ChargingSuspended(Known.Session).At(suspendedAt ?? Full),
            ],
            "ended" =>
            [
                Started(tariff),
                new ChargingSuspended(Known.Session).At(Full),
                new SessionEnded(Known.Session, Depot, Connector, DriversCard, new(27.6m))
                    .At(Full.AddMinutes(25)),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        });

    private static SessionEvent Started(TariffSnapshot tariff) =>
        new SessionStarted(Known.Session, Depot, Connector, DriversCard, tariff, Cap, 1_200_000)
            .At(Morning);

    private static Money Usd(decimal amount) => new(amount, Currency.Usd);
}
