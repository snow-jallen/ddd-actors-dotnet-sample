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

    [Fact]
    public void Given_an_origin_and_a_purpose_when_an_id_is_derived_then_it_is_the_same_every_time()
    {
        var origin = GuidV7.New(new FakeTimeProvider(PluggedInAt));

        GuidV7.Derive(origin, "release-allocation")
            .ShouldBe(GuidV7.Derive(origin, "release-allocation"));
    }

    [Fact]
    public void Given_another_purpose_or_origin_when_an_id_is_derived_then_it_differs()
    {
        var clock = new FakeTimeProvider(PluggedInAt);
        var origin = GuidV7.New(clock);
        var other = GuidV7.New(clock);

        GuidV7.Derive(origin, "release-allocation")
            .ShouldNotBe(GuidV7.Derive(origin, "unlock-token"));
        GuidV7.Derive(origin, "release-allocation")
            .ShouldNotBe(GuidV7.Derive(other, "release-allocation"));
    }

    [Fact]
    public void Given_an_origin_when_an_id_is_derived_then_it_is_version_7_with_the_origins_time()
    {
        var origin = GuidV7.New(new FakeTimeProvider(PluggedInAt));

        var derived = GuidV7.Derive(origin, "release-allocation");

        derived.Version.ShouldBe(7);
        (derived.Variant & 0b1100).ShouldBe(0b1000); // The RFC 9562 variant, 10xx.
        derived.ToString("N")[..12].ShouldBe(origin.ToString("N")[..12]);
        CommandId.TryCreate(derived).IsOk.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_cause_when_a_command_id_is_derived_then_it_is_the_derived_guid()
    {
        var cause = CommandId.New(new FakeTimeProvider(PluggedInAt));

        CommandId.Derive(cause, "unlock-token").Value
            .ShouldBe(GuidV7.Derive(cause.Value, "unlock-token"));
    }
}
