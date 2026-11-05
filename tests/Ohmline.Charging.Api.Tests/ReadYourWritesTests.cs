using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Api.Queries;

namespace Ohmline.Charging.Api.Tests;

/// <summary>
/// Read-your-writes against a read model that the test moves along by hand, on a fake clock:
/// no database, and no real waiting.
/// </summary>
public sealed class ReadYourWritesTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    private readonly WaitingClock _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_row_at_the_version_then_it_has_caught_up_at_the_first_read()
    {
        var history = new History(versions: [7]);

        var read = await ReadAsync(history, minVersion: 7);

        read.Value.ShouldBeOfType<CaughtUp<Row>>().Row.ShouldBe(new Row(7));
        history.Reads.ShouldBe(1);
    }

    [Fact]
    public async Task Given_a_newer_row_than_asked_for_then_it_has_caught_up()
    {
        var read = await ReadAsync(new History(versions: [9]), minVersion: 7);

        read.Value.ShouldBeOfType<CaughtUp<Row>>().Row.ShouldBe(new Row(9));
    }

    [Fact]
    public async Task Given_a_projection_that_catches_up_on_the_third_read_then_100_ms_later()
    {
        var history = new History(versions: [6, 6, 7]);

        var read = await ReadAsync(history, minVersion: 7);

        read.Value.ShouldBeOfType<CaughtUp<Row>>().Row.ShouldBe(new Row(7));
        history.Reads.ShouldBe(3);
        (_clock.GetUtcNow() - Morning).ShouldBe(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task Given_a_projection_that_never_catches_up_then_still_behind_after_2_s()
    {
        var history = new History(versions: [6]);

        var read = await ReadAsync(history, minVersion: 7);

        read.Value.ShouldBeOfType<StillBehind>().Version.ShouldBe(6);
        (_clock.GetUtcNow() - Morning).ShouldBe(TimeSpan.FromSeconds(2));
        history.Reads.ShouldBe(41); // At 0 ms, every 50 ms after it, and once more at 2 s.
    }

    [Fact]
    public async Task Given_no_row_yet_then_still_behind_at_version_0()
    {
        var read = await ReadAsync(new History(versions: [null]), minVersion: 1);

        read.Value.ShouldBeOfType<StillBehind>().Version.ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_timeout_of_zero_then_it_reads_once_and_never_waits()
    {
        var history = new History(versions: [6, 7]);

        var read = await ReadAsync(history, minVersion: 7, timeout: TimeSpan.Zero);

        read.Value.ShouldBeOfType<StillBehind>().Version.ShouldBe(6);
        history.Reads.ShouldBe(1);
    }

    /// <summary>Runs the read, moving the fake clock on whenever the helper waits.</summary>
    private async Task<CatchUp<Row>> ReadAsync(
        History history, long minVersion, TimeSpan? timeout = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ReadModelOptions
        {
            ReadYourWritesTimeout = timeout ?? TimeSpan.FromSeconds(2),
        });
        var reading = new ReadYourWrites(_clock, options)
            .ReadAsync(history.ReadAsync, row => row.Version, minVersion, Ct);
        while (await Task.WhenAny(reading, _clock.NextWaitAsync()) != reading)
        {
            _clock.Advance(ReadYourWrites.PollInterval);
        }

        return await reading;
    }

    private sealed record Row(long Version);

    /// <summary>
    /// A history row whose version moves along a script, one step per read; the last step
    /// stays. Null is "no row yet".
    /// </summary>
    private sealed class History(long?[] versions)
    {
        public int Reads { get; private set; }

        public Task<Row?> ReadAsync(CancellationToken cancellationToken)
        {
            var version = versions[Math.Min(Reads, versions.Length - 1)];
            Reads++;
            return Task.FromResult(version is { } found ? new Row(found) : null);
        }
    }

    /// <summary>A fake clock that counts the times someone starts waiting on it.</summary>
    private sealed class WaitingClock() : FakeTimeProvider(Morning)
    {
        private readonly SemaphoreSlim _waits = new(0);

        /// <summary>Completes for each wait that has started, one at a time.</summary>
        public Task NextWaitAsync() => _waits.WaitAsync(Ct);

        public override ITimer CreateTimer(
            TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _waits.Release();
            return timer;
        }
    }
}
