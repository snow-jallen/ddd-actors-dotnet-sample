using Ohmline.Tariffs.Contracts;
using static Ohmline.Tariffs.Domain.Tests.TheDepotTariff;

namespace Ohmline.Tariffs.Domain.Tests;

public sealed class TimeOfUseBandTests
{
    [Theory]
    [InlineData("15:59", false)]
    [InlineData("16:00", true)]
    [InlineData("20:59", true)]
    [InlineData("21:00", false)]
    public void Given_a_daytime_band_then_it_holds_its_start_but_not_its_end(
        string time, bool inside)
    {
        Peak.Contains(TimeOnly.Parse(time)).ShouldBe(inside);
    }

    [Theory]
    [InlineData("21:59", false)]
    [InlineData("22:00", true)]
    [InlineData("00:00", true)]
    [InlineData("05:59", true)]
    [InlineData("06:00", false)]
    public void Given_a_band_past_midnight_then_it_holds_the_late_evening_and_the_early_morning(
        string time, bool inside)
    {
        Night.Contains(TimeOnly.Parse(time)).ShouldBe(inside);
    }

    [Fact]
    public void Given_bands_that_share_any_minute_then_they_overlap_whichever_runs_past_midnight()
    {
        var lateEvening = new TimeOfUseBand("late", new TimeOnly(21, 0), new TimeOnly(23, 0));
        var dawn = new TimeOfUseBand("dawn", new TimeOnly(5, 0), new TimeOnly(7, 0));

        Night.Overlaps(lateEvening).ShouldBeTrue();
        dawn.Overlaps(Night).ShouldBeTrue();
        Peak.Overlaps(Night).ShouldBeFalse();
        Peak.Overlaps(lateEvening).ShouldBeFalse();
    }
}
