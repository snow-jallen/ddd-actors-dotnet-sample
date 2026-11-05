using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Time;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

/// <summary>
/// A reservation's fifteen minutes, on a fake clock. The test plays the shell: it reads the
/// clock once for each command, as <see cref="DecisionContext.From"/>, and moves the clock on
/// between commands. The decider never sees the clock, only the reading.
/// </summary>
public sealed class ReservationExpiryTests
{
    private static readonly ChargePointRegistered Registration = new(Charger, Depot, [new(50m)]);

    /// <summary>The depot's morning. A test may start it elsewhere, never wind it back.</summary>
    private FakeTimeProvider clock = new(Morning);

    [Fact]
    public void Given_a_reservation_then_another_driver_is_refused_up_to_its_last_second()
    {
        var reserved = Reserve(DriversCard);

        clock.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));

        Given(reserved)
            .When(new ReserveConnector(Command, Charger, 1, SomeoneElsesCard))
            .ThenRejects(new ChargePointError.ReservedForAnotherToken(1, Morning.AddMinutes(15)));
    }

    [Fact]
    public void Given_a_reservation_when_its_time_is_up_then_another_driver_may_reserve()
    {
        var reserved = Reserve(DriversCard);

        clock.Advance(TimeSpan.FromMinutes(15));

        var now = clock.GetUtcNow();
        Given(reserved)
            .When(new ReserveConnector(Command, Charger, 1, SomeoneElsesCard))
            .Then(
                new ReservationExpired(Charger, 1, DriversCard).At(now),
                new ConnectorReserved(Charger, 1, SomeoneElsesCard, now.AddMinutes(15)).At(now));
    }

    [Fact]
    public void Given_a_reservation_when_the_timer_fires_early_then_it_waits_for_the_real_time()
    {
        var reserved = Reserve(DriversCard);

        clock.Advance(TimeSpan.FromMinutes(14));
        Given(reserved).When(new ExpireReservation(Command, Charger, 1)).ThenNothingChanges();

        clock.Advance(TimeSpan.FromMinutes(1));
        Given(reserved)
            .When(new ExpireReservation(Command, Charger, 1))
            .Then(new ReservationExpired(Charger, 1, DriversCard).At(clock.GetUtcNow()));
    }

    [Fact]
    public void Given_a_reservation_replayed_a_day_later_then_its_expiry_is_the_recorded_one()
    {
        var reserved = Reserve(DriversCard);

        clock.Advance(TimeSpan.FromDays(1));

        var connector = ChargePoint.Fold([Registration, .. reserved]).Value
            .ShouldBeOfType<Registered>().Connectors[1];
        connector.State.Value.ShouldBe(new Reserved(DriversCard, Morning.AddMinutes(15)));
    }

    [Fact]
    public void Given_a_reservation_as_the_clocks_go_back_then_it_lasts_fifteen_real_minutes()
    {
        // 01:50 MDT on 1 November 2026 at the depot; ten minutes later it is 01:00 MST.
        clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 7, 50, 0, TimeSpan.Zero));
        var reserved = Reserve(DriversCard);
        var expiry = reserved.OfType<ConnectorReserved>().Single().ExpiresAt;

        var wallClock = SiteLocalTime.Of(expiry, "America/Denver");
        (wallClock.TimeOfDay, wallClock.IsDaylightSavingTime).ShouldBe((new TimeOnly(1, 5), false));

        clock.Advance(TimeSpan.FromMinutes(15));
        Given(reserved)
            .When(new ExpireReservation(Command, Charger, 1))
            .Then(new ReservationExpired(Charger, 1, DriversCard).At(expiry));
    }

    /// <summary>Reserves connector 1 now; returns what the charge point recorded.</summary>
    private ChargePointEvent[] Reserve(TokenUid token)
    {
        var now = clock.GetUtcNow();
        return
        [
            .. Given()
                .When(new ReserveConnector(Command, Charger, 1, token))
                .Then(new ConnectorReserved(Charger, 1, token, now.AddMinutes(15)).At(now)),
        ];
    }

    /// <summary>The registered charge point and its history, at the clock's reading.</summary>
    private ChargePointSpec Given(params IEnumerable<ChargePointEvent> history) =>
        ChargePointSpec.Given([Registration, .. history]).With(DecisionContext.From(clock));
}
