using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.SharedKernel.Tests.Identity;

public sealed class GuidV7Tests
{
    private static readonly DateTimeOffset PluggedInAt = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_clock_when_an_id_is_created_then_it_is_version_7_with_the_clocks_time()
    {
        var clock = new FakeTimeProvider(PluggedInAt);

        var id = GuidV7.New(clock);

        id.Version.ShouldBe(7);
        var unixMilliseconds = Convert.ToInt64(id.ToString("N")[..12], fromBase: 16);
        unixMilliseconds.ShouldBe(PluggedInAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Given_one_instant_when_two_ids_are_created_then_they_still_differ()
    {
        var clock = new FakeTimeProvider(PluggedInAt);

        GuidV7.New(clock).ShouldNotBe(GuidV7.New(clock));
    }

    [Fact]
    public void Given_a_clock_moving_forward_when_ids_are_created_then_they_sort_in_that_order()
    {
        var clock = new FakeTimeProvider(PluggedInAt);
        var earlier = GuidV7.New(clock);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        var later = GuidV7.New(clock);

        earlier.CompareTo(later).ShouldBeLessThan(0);
        string.CompareOrdinal(earlier.ToString(), later.ToString()).ShouldBeLessThan(0);
    }
}
