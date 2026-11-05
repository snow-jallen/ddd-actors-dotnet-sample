using Microsoft.Extensions.Time.Testing;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Outbox;
using Ohmline.Mobile.Core.ReadModels;
using Ohmline.Mobile.Core.Tests.Outbox;
using static Ohmline.Mobile.Core.Tests.ReadModels.Cards;

namespace Ohmline.Mobile.Core.Tests.ReadModels;

public sealed class SessionCardViewModelTests : IAsyncDisposable
{
    private readonly TemporaryDatabase _database = new();
    private readonly FakeSender _server = new();
    private readonly FakeLiveUpdates _live = new();
    private readonly SqliteReadModelCache _cache;
    private readonly OutboxDispatcher _dispatcher;
    private readonly SessionCardViewModel _screen;

    public SessionCardViewModelTests()
    {
        var clock = new FakeTimeProvider(Morning);
        var outbox = new SqliteOutbox(_database.ConnectionString);
        _cache = new SqliteReadModelCache(_database.ConnectionString);
        _dispatcher = new OutboxDispatcher(
            outbox, _server, new FakeConnectivity(), new InlineDispatcher(), clock);
        _screen = new SessionCardViewModel(
            _cache, outbox, _dispatcher, _live, new DriverToken(Token), clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_stop_when_the_pushed_card_catches_up_then_the_overlay_goes()
    {
        await _cache.ApplyAsync(At(2, 7.35m), position: 2, Ct);
        await _screen.RefreshCommand.ExecuteAsync(null);
        _server.Then(new SendAccepted(3));

        await _screen.StopChargingCommand.ExecuteAsync(null);
        var queued = _screen.Pending;
        _dispatcher.Start();
        await _dispatcher.FlushAsync();
        await _screen.RefreshCommand.ExecuteAsync(null);
        var accepted = _screen.Pending;
        await _cache.ApplyAsync(At(3, 12m, ended: true), position: 3, Ct); // The push.
        await _screen.RefreshCommand.ExecuteAsync(null);

        (queued, accepted).ShouldBe((SessionCardView.Stopping, SessionCardView.Stopping));
        _screen.Pending.ShouldBeNull();
        _screen.Card.ShouldBe(At(3, 12m, ended: true));
        _server.Sent.ShouldHaveSingleItem().AggregateKey.ShouldBe(Session.ToString());
    }

    [Fact]
    public async Task Given_a_rejected_stop_then_the_card_says_why_in_the_drivers_words()
    {
        await _cache.ApplyAsync(At(2, 7.35m), position: 2, Ct);
        await _screen.RefreshCommand.ExecuteAsync(null);
        _server.Then(new SendRejected("already-ended", "Session already ended."));

        await _screen.StopChargingCommand.ExecuteAsync(null);
        _dispatcher.Start();
        await _dispatcher.FlushAsync();
        await _screen.RefreshCommand.ExecuteAsync(null);

        _screen.Pending.ShouldBeNull();
        _screen.Problem.ShouldBe("This charge has already ended.");
    }

    [Fact]
    public async Task Given_a_change_in_the_cache_then_the_card_is_read_again()
    {
        await _cache.ApplyAsync(At(1), position: 1, Ct);

        _live.ChangeCards();

        await AwaitAsync(() => _screen.Card == At(1));
    }

    public async ValueTask DisposeAsync()
    {
        await _dispatcher.StopAsync();
        _database.Dispose();
    }

    private static async Task AwaitAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "The screen did not refresh in time.");
            await Task.Delay(10, Ct);
        }
    }
}
