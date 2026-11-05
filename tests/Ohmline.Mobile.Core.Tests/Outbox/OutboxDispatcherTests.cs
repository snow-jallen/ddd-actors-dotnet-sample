using Microsoft.Extensions.Time.Testing;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Outbox;
using static Ohmline.Mobile.Core.Tests.Outbox.Commands;

namespace Ohmline.Mobile.Core.Tests.Outbox;

/// <summary>
/// The dispatcher, drained by the test (<see cref="OutboxDispatcher.FlushAsync"/>) with a fake
/// server, a fake network and a fake clock: nothing waits for real time.
/// </summary>
public sealed class OutboxDispatcherTests : IAsyncDisposable
{
    private readonly TemporaryDatabase _database = new();
    private readonly FakeSender _server = new();
    private readonly FakeConnectivity _network = new();
    private readonly FakeTimeProvider _clock = new(Morning);
    private readonly List<(Guid, CommandStatus)> _statuses = [];
    private readonly SqliteOutbox _outbox;
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherTests()
    {
        _outbox = new SqliteOutbox(_database.ConnectionString);
        _dispatcher = new OutboxDispatcher(
            _outbox, _server, _network, new InlineDispatcher(), _clock);
        _dispatcher.StatusChanged += (id, status) => _statuses.Add((id, status));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_queued_command_then_it_is_sent_and_its_version_kept()
    {
        var command = await EnqueueAsync();
        _server.Then(new SendAccepted(1));

        await DrainAsync();

        _server.Sent.ShouldHaveSingleItem().CommandId.ShouldBe(command.CommandId);
        (await _outbox.FindAsync(command.CommandId, Ct))!.ToStatus().Value
            .ShouldBe(new Accepted(1));
        _statuses.Select(change => change.Item2.Value)
            .ShouldBe([new Sending(1), new Accepted(1)]);
    }

    [Fact]
    public async Task Given_a_transient_failure_then_the_same_command_id_is_sent_again()
    {
        var command = await EnqueueAsync();
        _server.Then(new SendTransientFailure("HTTP 504")).Then(new SendAccepted(1));

        await DrainAsync();
        await _dispatcher.FlushAsync();

        _server.Sent.Select(row => row.CommandId).ShouldBe([command.CommandId, command.CommandId]);
        _server.Sent.Select(row => row.Payload).Distinct().ShouldHaveSingleItem();
        (await _outbox.FindAsync(command.CommandId, Ct))!.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Given_a_transient_failure_then_the_commands_after_it_wait_their_turn()
    {
        var (first, second) = (await EnqueueAsync(), await EnqueueAsync());
        _server.Then(new SendTransientFailure("no network"));

        await DrainAsync();
        var sentFirst = _server.Sent.Select(row => row.CommandId).ToList();
        await _dispatcher.FlushAsync();

        sentFirst.ShouldBe([first.CommandId]);
        _server.Sent.Select(row => row.CommandId)
            .ShouldBe([first.CommandId, first.CommandId, second.CommandId]);
    }

    [Fact]
    public async Task Given_a_rejection_then_it_is_final_and_the_next_command_is_still_sent()
    {
        var (refused, next) = (await EnqueueAsync(), await EnqueueAsync());
        _server.Then(new SendRejected("reserved-for-another-token", "Connector 1 is reserved."));

        await DrainAsync();
        await _dispatcher.FlushAsync();

        _server.Sent.Select(row => row.CommandId).ShouldBe([refused.CommandId, next.CommandId]);
        var row = (await _outbox.FindAsync(refused.CommandId, Ct))!;
        (row.Status, row.Code, row.Reason).ShouldBe(
            ("rejected", "reserved-for-another-token", "Connector 1 is reserved."));
        _statuses.ShouldContain((refused.CommandId, new Rejected(
            "reserved-for-another-token", "This connector is reserved for another driver.")));
    }

    [Fact]
    public async Task Given_a_command_older_than_24_hours_then_it_fails_to_sync_unsent()
    {
        var stale = await EnqueueAsync();
        _clock.Advance(OutboxDispatcher.MaxAge + TimeSpan.FromSeconds(1));
        var fresh = await EnqueueAsync();

        await DrainAsync();

        _server.Sent.Select(row => row.CommandId).ShouldBe([fresh.CommandId]);
        (await _outbox.FindAsync(stale.CommandId, Ct))!.Status.ShouldBe("failed-to-sync");
        _statuses.ShouldContain((stale.CommandId, new FailedToSync()));
    }

    [Fact]
    public async Task Given_no_network_then_nothing_is_sent_until_it_comes_back()
    {
        _network.IsOnline = false;
        var command = await EnqueueAsync();

        await DrainAsync();
        var sentOffline = _server.Sent.Count;
        _network.IsOnline = true; // The change nudges the dispatcher.
        await _dispatcher.FlushAsync();

        sentOffline.ShouldBe(0);
        _server.Sent.ShouldHaveSingleItem().CommandId.ShouldBe(command.CommandId);
    }

    [Fact]
    public async Task Given_a_transient_failure_then_the_timer_tries_again_after_the_backoff()
    {
        var command = await EnqueueAsync();
        _server.Then(new SendTransientFailure("HTTP 503"));
        await DrainAsync();

        _clock.Advance(OutboxDispatcher.Backoff(failures: 1, jitter: 1));

        await AwaitAsync(() => _server.Sent.Count == 2);
        _server.Sent[1].CommandId.ShouldBe(command.CommandId);
    }

    [Theory]
    [InlineData(1, 1.6, 2.4)]
    [InlineData(2, 3.2, 4.8)]
    [InlineData(5, 25.6, 38.4)]
    [InlineData(20, 240, 360)]
    public void The_backoff_doubles_up_to_5_minutes_spread_by_a_fifth(
        int failures, double fewestSeconds, double mostSeconds)
    {
        OutboxDispatcher.Backoff(failures, jitter: 0).TotalSeconds
            .ShouldBe(fewestSeconds, tolerance: 0.001);
        OutboxDispatcher.Backoff(failures, jitter: 0.999_999).TotalSeconds
            .ShouldBe(mostSeconds, tolerance: 0.001);
    }

    public async ValueTask DisposeAsync()
    {
        await _dispatcher.StopAsync();
        _database.Dispose();
    }

    private async Task<OutboxCommand> EnqueueAsync()
    {
        var command = StartCharging();
        await _outbox.EnqueueAsync(command, _clock.GetUtcNow(), Ct);
        return command;
    }

    /// <summary>Starts the dispatcher, which drains at once, and waits for a drain.</summary>
    private async Task DrainAsync()
    {
        _dispatcher.Start();
        await _dispatcher.FlushAsync();
    }

    private static async Task AwaitAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "The dispatcher did not send in time.");
            await Task.Delay(10, Ct);
        }
    }
}
