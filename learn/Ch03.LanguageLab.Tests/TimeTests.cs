using Microsoft.Extensions.Time.Testing;

namespace Ch03.LanguageLab.Tests;

public sealed class TimeTests
{
    private static readonly DateTimeOffset PluggedInAt = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_fake_clock_when_time_advances_then_the_reservation_expires()
    {
        var clock = new FakeTimeProvider(PluggedInAt);
        var reservation = new ReservationTimer(clock, holdFor: TimeSpan.FromMinutes(15));

        clock.Advance(TimeSpan.FromMinutes(14));
        reservation.HasExpired.ShouldBeFalse();

        clock.Advance(TimeSpan.FromMinutes(1));
        reservation.HasExpired.ShouldBeTrue();
        reservation.ExpiresAt.ShouldBe(PluggedInAt.AddMinutes(15));
    }

    [Fact]
    public void Given_a_fake_clock_when_advanced_then_its_timers_fire_without_waiting()
    {
        var clock = new FakeTimeProvider(PluggedInAt);
        var meterRequests = 0;
        using var timer = clock.CreateTimer(
            _ => meterRequests++, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        clock.Advance(TimeSpan.FromMinutes(3));

        meterRequests.ShouldBe(3);
    }

    [Fact]
    public async Task Given_a_fake_clock_when_a_delay_is_awaited_then_advancing_completes_it()
    {
        var clock = new FakeTimeProvider(PluggedInAt);
        var gracePeriod = Task.Delay(
            TimeSpan.FromMinutes(15), clock, TestContext.Current.CancellationToken);

        gracePeriod.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMinutes(15));

        await gracePeriod;
    }

    [Fact]
    public void Given_a_clock_when_a_version_7_guid_is_created_then_it_carries_the_time()
    {
        var clock = new FakeTimeProvider(PluggedInAt);

        var id = Guid.CreateVersion7(clock.GetUtcNow());

        id.Version.ShouldBe(7);
        var unixMilliseconds = Convert.ToInt64(id.ToString("N")[..12], fromBase: 16);
        DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ShouldBe(PluggedInAt);
    }

    [Fact]
    public void Given_ids_created_as_time_passes_then_they_sort_in_creation_order()
    {
        var clock = new FakeTimeProvider(PluggedInAt);
        var ids = new List<Guid>();
        for (var minute = 0; minute < 5; minute++)
        {
            ids.Add(Guid.CreateVersion7(clock.GetUtcNow()));
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        ids.Order().ShouldBe(ids);
    }
}
