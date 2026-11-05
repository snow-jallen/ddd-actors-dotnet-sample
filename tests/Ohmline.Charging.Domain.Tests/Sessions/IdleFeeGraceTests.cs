using System.Collections.Immutable;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Policies;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Time;
using Ohmline.Tariffs.Contracts;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Sessions;

/// <summary>
/// The idle fee's grace period on a fake clock: USD 0.40 a minute once the car has stood
/// fifteen minutes after it stopped charging. The test plays the shell, reading the clock once
/// for each command; the rule itself is asked about a moment, never about "now".
/// </summary>
public sealed class IdleFeeGraceTests
{
    private static readonly IdleFee Idle =
        new(new Money(0.40m, Currency.Usd), TimeSpan.FromMinutes(15));

    /// <summary>The depot's morning. A test may start it elsewhere, never wind it back.</summary>
    private FakeTimeProvider clock = new(Morning);

    [Fact]
    public void Given_a_car_that_stopped_charging_then_the_fee_applies_once_the_grace_is_over()
    {
        var history = Started();
        clock.Advance(TimeSpan.FromMinutes(48));
        history = Decide(history, new SuspendCharging(Command, Session, new ByEv()));

        clock.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));
        IdleFeeAppliesNow(history).ShouldBeFalse();

        clock.Advance(TimeSpan.FromSeconds(1));
        IdleFeeAppliesNow(history).ShouldBeTrue();
    }

    [Fact]
    public void Given_the_charge_point_stopped_charging_then_no_fee_applies_however_long_it_waits()
    {
        var history = Started();
        clock.Advance(TimeSpan.FromMinutes(48));
        history = Decide(history, new SuspendCharging(Command, Session, new ByCharger()));

        clock.Advance(TimeSpan.FromHours(1));

        IdleFeeAppliesNow(history).ShouldBeFalse();
    }

    [Fact]
    public void Given_charging_that_resumes_within_the_grace_period_then_the_next_stop_starts_anew()
    {
        var history = Started();
        clock.Advance(TimeSpan.FromMinutes(48));
        history = Decide(history, new SuspendCharging(Command, Session, new ByEv()));
        clock.Advance(TimeSpan.FromMinutes(10));
        history = Decide(history, new ResumeCharging(Command, Session));
        clock.Advance(TimeSpan.FromMinutes(5));
        history = Decide(history, new SuspendCharging(Command, Session, new ByEv()));

        clock.Advance(TimeSpan.FromMinutes(10));
        IdleFeeAppliesNow(history).ShouldBeFalse();

        clock.Advance(TimeSpan.FromMinutes(5));
        IdleFeeAppliesNow(history).ShouldBeTrue();
    }

    [Fact]
    public void Given_a_warning_of_the_idle_fee_then_it_names_the_moment_the_fee_starts_to_apply()
    {
        var history = Started();
        clock.Advance(TimeSpan.FromMinutes(48));
        history = Decide(history, new SuspendCharging(Command, Session, new ByEv()));

        var warning = new IdleFeePolicy(Idle).React(history[^1]).Single().Value
            .ShouldBeOfType<ChargingIntent.WarnOfIdleFee>();

        var session = ChargingSession.Fold(history);
        SessionPredicates.IdleFeeApplies(session, warning.StartsAt.AddTicks(-1)).ShouldBeFalse();
        SessionPredicates.IdleFeeApplies(session, warning.StartsAt).ShouldBeTrue();
    }

    [Fact]
    public void Given_a_car_that_stops_as_the_clocks_go_back_then_the_grace_is_real_minutes()
    {
        // 01:50 MDT on 1 November 2026 at the depot; ten minutes later it is 01:00 MST.
        clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 7, 50, 0, TimeSpan.Zero));
        var history = Started();
        history = Decide(history, new SuspendCharging(Command, Session, new ByEv()));

        var warning = new IdleFeePolicy(Idle).React(history[^1]).Single().Value
            .ShouldBeOfType<ChargingIntent.WarnOfIdleFee>();
        var wallClock = SiteLocalTime.Of(warning.StartsAt, "America/Denver");
        (wallClock.TimeOfDay, wallClock.IsDaylightSavingTime).ShouldBe((new TimeOnly(1, 5), false));

        clock.Advance(TimeSpan.FromMinutes(15));
        IdleFeeAppliesNow(history).ShouldBeTrue();
    }

    /// <summary>Started at the clock's reading, under a tariff with an idle fee.</summary>
    private SessionEvent[] Started()
    {
        var tariff = StandardTariff(0.42m) with
        {
            Components = [new EnergyRate(new Money(0.42m, Currency.Usd)), Idle],
        };

        return
        [
            new SessionStarted(
                Session, Depot, new ConnectorId(Charger, 1), DriversCard, tariff,
                new Money(40m, Currency.Usd), 1_200_000).At(clock.GetUtcNow()),
        ];
    }

    /// <summary>Decides the command at the clock's reading; returns the longer history.</summary>
    private SessionEvent[] Decide(SessionEvent[] history, SessionCommand command) =>
        SessionSpec.Given(history).With(DecisionContext.From(clock)).When(command).Result
            is Ok<ImmutableArray<SessionEvent>> accepted
            ? [.. history, .. accepted.Value]
            : throw new InvalidOperationException($"{command} was refused.");

    private bool IdleFeeAppliesNow(SessionEvent[] history) =>
        new IdleFeeApplies(clock.GetUtcNow()).IsSatisfiedBy(ChargingSession.Fold(history));
}
