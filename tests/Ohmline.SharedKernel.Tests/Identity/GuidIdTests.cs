using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.SharedKernel.Tests.Identity;

public sealed class GuidIdTests
{
    private static readonly DateTimeOffset PluggedInAt = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_clock_when_a_session_id_is_created_then_it_carries_the_clocks_time()
    {
        var id = SessionId.New(new FakeTimeProvider(PluggedInAt));

        id.Value.Version.ShouldBe(7);
        var unixMilliseconds = Convert.ToInt64(id.Value.ToString("N")[..12], 16);
        DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ShouldBe(PluggedInAt);
    }

    [Fact]
    public void Given_a_clock_moving_forward_when_journeys_start_then_their_ids_sort_in_order()
    {
        var clock = new FakeTimeProvider(PluggedInAt);
        var first = JourneyId.New(clock);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var second = JourneyId.New(clock);

        first.Value.CompareTo(second.Value).ShouldBeLessThan(0);
    }

    [Fact]
    public void Given_a_version_7_guid_when_trying_to_create_then_it_is_an_id()
    {
        var raw = Guid.CreateVersion7(PluggedInAt);

        SessionId.TryCreate(raw).ShouldBe(Result.Ok(new SessionId(raw)));
        JourneyId.TryCreate(raw).ShouldBe(Result.Ok(new JourneyId(raw)));
    }

    [Fact]
    public void Given_a_random_version_4_guid_when_trying_to_create_then_it_is_malformed()
    {
        var raw = Guid.Parse("3f2b8c4e-9d1a-4e6b-8f0c-2a7d5e9b1c34");

        SessionId.TryCreate(raw).ShouldBe(Result.Fail<ValidationError>(
            new Malformed("SessionId", raw.ToString(), "a version 7 Guid")));
    }

    [Fact]
    public void Given_the_empty_guid_when_trying_to_create_then_it_is_missing()
    {
        JourneyId.TryCreate(Guid.Empty)
            .ShouldBe(Result.Fail<ValidationError>(new Missing("JourneyId")));
    }

    [Fact]
    public void Given_a_guid_that_is_not_version_7_when_constructed_then_the_guard_throws()
    {
        Should.Throw<ArgumentException>(() => new SessionId(Guid.Empty));
    }

    [Fact]
    public void Given_a_clock_when_a_command_id_is_created_then_it_is_ordered_by_its_time()
    {
        var clock = new FakeTimeProvider(PluggedInAt);
        var first = CommandId.New(clock);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var retried = first with { };

        first.Value.Version.ShouldBe(7);
        first.Value.CompareTo(CommandId.New(clock).Value).ShouldBeLessThan(0);
        retried.ShouldBe(first);
    }

    [Fact]
    public void Given_a_random_version_4_guid_when_trying_to_create_a_command_id_it_is_malformed()
    {
        var raw = Guid.Parse("3f2b8c4e-9d1a-4e6b-8f0c-2a7d5e9b1c34");

        CommandId.TryCreate(raw).ShouldBe(Result.Fail<ValidationError>(
            new Malformed("CommandId", raw.ToString(), "a version 7 Guid")));
    }

    [Fact]
    public void Given_one_guid_then_a_session_id_and_a_journey_id_are_different_types()
    {
        var raw = Guid.CreateVersion7(PluggedInAt);

        new SessionId(raw).Equals(new JourneyId(raw)).ShouldBeFalse();
        new SessionId(raw).ToString().ShouldBe(raw.ToString());
    }
}
