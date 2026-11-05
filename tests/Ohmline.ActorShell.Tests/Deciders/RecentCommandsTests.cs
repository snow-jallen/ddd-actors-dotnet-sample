using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.ActorShell.Tests.Deciders;

public sealed class RecentCommandsTests
{
    private static readonly CommandId[] Ids =
    [
        .. Enumerable.Range(1, 5).Select(n =>
            new CommandId(Guid.Parse($"019a4f6e-1c00-7d2a-9b3e-5c0a0000000{n}"))),
    ];

    [Fact]
    public void The_window_holds_256_commands_by_default() =>
        RecentCommands.DefaultCapacity.ShouldBe(256);

    [Fact]
    public void Given_a_recorded_command_then_its_version_is_found()
    {
        var recent = new RecentCommands();
        recent.Record(Ids[0], 7);

        recent.TryGetVersion(Ids[0], out var version).ShouldBeTrue();
        version.ShouldBe(7);
        recent.TryGetVersion(Ids[1], out _).ShouldBeFalse();
    }

    [Fact]
    public void Given_a_command_that_stored_several_events_then_its_last_one_is_its_version()
    {
        var recent = new RecentCommands();
        recent.Record(Ids[0], 3);
        recent.Record(Ids[0], 4);

        recent.TryGetVersion(Ids[0], out var version).ShouldBeTrue();
        version.ShouldBe(4);
        recent.Count.ShouldBe(1);
    }

    [Fact]
    public void Given_a_full_window_when_another_command_is_recorded_then_the_oldest_goes()
    {
        var recent = new RecentCommands(capacity: 3);
        foreach (var (id, version) in Ids[..4].Select((id, index) => (id, index + 1L)))
        {
            recent.Record(id, version);
        }

        recent.Count.ShouldBe(3);
        recent.TryGetVersion(Ids[0], out _).ShouldBeFalse();
        recent.TryGetVersion(Ids[3], out _).ShouldBeTrue();
    }

    [Fact]
    public void Given_a_command_sent_again_then_it_is_forgotten_last()
    {
        var recent = new RecentCommands(capacity: 3);
        recent.Record(Ids[0], 1);
        recent.Record(Ids[1], 2);
        recent.Record(Ids[2], 3);

        recent.TryGetVersion(Ids[0], out _); // A retry: now the most recently used.
        recent.Record(Ids[3], 4);

        recent.TryGetVersion(Ids[0], out _).ShouldBeTrue();
        recent.TryGetVersion(Ids[1], out _).ShouldBeFalse();
    }

    [Fact]
    public void Given_a_window_restored_from_its_snapshot_then_it_forgets_in_the_same_order()
    {
        var recent = new RecentCommands(capacity: 3);
        recent.Record(Ids[0], 1);
        recent.Record(Ids[1], 2);
        recent.Record(Ids[2], 3);
        recent.TryGetVersion(Ids[0], out _);

        var restored = new RecentCommands(capacity: 3);
        restored.Restore(recent.ToSnapshot());
        restored.Record(Ids[3], 4);

        recent.ToSnapshot().ShouldBe(
            [new RecentCommand(Ids[1], 2), new RecentCommand(Ids[2], 3), new(Ids[0], 1)]);
        restored.TryGetVersion(Ids[1], out _).ShouldBeFalse();
        restored.TryGetVersion(Ids[0], out _).ShouldBeTrue();
    }
}
