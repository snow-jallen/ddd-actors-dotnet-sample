using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Time;

namespace Ohmline.SharedKernel.Tests.Time;

/// <summary>
/// The depot's wall clock in 2026. Denver springs forward at 02:00 on 8 March (09:00 UTC) and
/// falls back at 02:00 on 1 November (08:00 UTC). A fake clock set to Denver's time zone checks
/// every conversion against .NET's own local time.
/// </summary>
public sealed class SiteLocalTimeTests
{
    private const string Denver = "America/Denver";

    private static readonly TimeZoneInfo DenverZone = TimeZoneInfo.FindSystemTimeZoneById(Denver);

    [Fact]
    public void Given_a_november_morning_then_the_depot_is_on_mountain_standard_time()
    {
        var instant = new DateTimeOffset(2026, 11, 4, 14, 52, 0, TimeSpan.Zero);

        var local = SiteLocalTime.Of(instant, Denver);

        local.Local.ShouldBe(new DateTimeOffset(2026, 11, 4, 7, 52, 0, TimeSpan.FromHours(-7)));
        local.TimeOfDay.ShouldBe(new TimeOnly(7, 52));
        local.Date.ShouldBe(new DateOnly(2026, 11, 4));
        local.IsDaylightSavingTime.ShouldBeFalse();
    }

    [Fact]
    public void Given_the_night_the_clocks_go_forward_then_two_oclock_never_shows_on_the_wall()
    {
        var clock = DenverClock(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero));
        var wallClock = new List<TimeOnly>();

        for (var minute = 0; minute < 4 * 60; minute++)
        {
            var local = SiteLocalTime.Of(clock.GetUtcNow(), Denver);
            local.Local.ShouldBe(clock.GetLocalNow());
            wallClock.Add(local.TimeOfDay);
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        wallClock.ShouldNotContain(time => time.Hour == 2);
        wallClock[119].ShouldBe(new TimeOnly(1, 59));
        wallClock[120].ShouldBe(new TimeOnly(3, 0));
    }

    [Fact]
    public void Given_the_night_the_clocks_go_back_then_the_hour_after_one_happens_twice()
    {
        var clock = DenverClock(new DateTimeOffset(2026, 11, 1, 6, 0, 0, TimeSpan.Zero));
        var wallClock = new List<SiteLocalTime>();

        for (var minute = 0; minute < 4 * 60; minute++)
        {
            var local = SiteLocalTime.Of(clock.GetUtcNow(), Denver);
            local.Local.ShouldBe(clock.GetLocalNow());
            wallClock.Add(local);
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        var oneOClock = wallClock.Where(local => local.TimeOfDay.Hour == 1).ToList();
        oneOClock.Count.ShouldBe(120);
        oneOClock.Count(local => local.IsDaylightSavingTime).ShouldBe(60);
        (oneOClock[^1].Instant - oneOClock[0].Instant).ShouldBe(TimeSpan.FromMinutes(119));
    }

    [Fact]
    public void Given_one_instant_then_two_sites_agree_on_it_but_not_on_their_wall_clocks()
    {
        var instant = new DateTimeOffset(2026, 11, 4, 14, 52, 0, TimeSpan.Zero);

        var depot = SiteLocalTime.Of(instant, Denver);
        var berlin = SiteLocalTime.Of(instant, "Europe/Berlin");

        depot.Instant.ShouldBe(berlin.Instant);
        depot.TimeOfDay.ShouldBe(new TimeOnly(7, 52));
        berlin.TimeOfDay.ShouldBe(new TimeOnly(15, 52));
    }

    [Theory]
    [InlineData("", "Time zone is missing.")]
    [InlineData("MST7MDT-ish", "Time zone must be an IANA time zone like America/Denver, "
        + "not 'MST7MDT-ish'.")]
    public void Given_no_usable_time_zone_then_there_is_no_local_time(string zone, string message)
    {
        SiteLocalTime.TryCreate(DateTimeOffset.UnixEpoch, zone)
            .Match(_ => "a local time", error => error.Message)
            .ShouldBe(message);
    }

    private static FakeTimeProvider DenverClock(DateTimeOffset start)
    {
        var clock = new FakeTimeProvider(start);
        clock.SetLocalTimeZone(DenverZone);
        return clock;
    }
}
