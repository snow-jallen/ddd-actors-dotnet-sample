using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Client.Contracts.Requests;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Outbox;

namespace Ohmline.Mobile.Core.ReadModels;

/// <summary>
/// The driver's session card (section 19.7): read from the cache, never from the network, so
/// it shows at once and offline, and read again whenever a push, a catch-up or the outbox
/// changes something. Stopping goes through the outbox like any command.
/// </summary>
public sealed partial class SessionCardViewModel : ObservableObject
{
    private readonly SqliteReadModelCache _cache;
    private readonly SqliteOutbox _outbox;
    private readonly OutboxDispatcher _dispatcher;
    private readonly DriverToken _token;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _refreshing = new(1, 1);

    public SessionCardViewModel(
        SqliteReadModelCache cache,
        SqliteOutbox outbox,
        OutboxDispatcher dispatcher,
        ILiveUpdates live,
        DriverToken token,
        TimeProvider clock)
    {
        (_cache, _outbox, _dispatcher, _token, _clock) = (cache, outbox, dispatcher, token, clock);
        live.CardsChanged += () => RefreshCommand.Execute(null);
        dispatcher.StatusChanged += (_, _) => RefreshCommand.Execute(null);
    }

    [ObservableProperty]
    public partial DriverSessionCard? Card { get; set; }

    [ObservableProperty]
    public partial string? Pending { get; set; }

    [ObservableProperty]
    public partial string? Problem { get; set; }

    /// <summary>
    /// Reads the cache and the outbox again. One refresh at a time, each reading anew, so that
    /// the last one to finish shows the latest of both, however the pushes and answers overlap.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshing.WaitAsync(cancellationToken);
        try
        {
            var cards = await _cache.ReadCardsAsync(_token.Uid, cancellationToken);
            var commands = await _outbox.ReadSinceAsync(
                _clock.GetUtcNow() - OutboxDispatcher.MaxAge, cancellationToken);
            (Card, Pending, Problem) = SessionCardView.Merge(cards, commands);
        }
        finally
        {
            _refreshing.Release();
        }
    }

    /// <summary>Stops the session on the card: a command, kept in the outbox until sent.</summary>
    [RelayCommand]
    private async Task StopChargingAsync(CancellationToken cancellationToken)
    {
        if (Card is not { EndedAt: null } charging)
        {
            return;
        }

        var now = _clock.GetUtcNow();
        var stop = new StopChargingRequest(Guid.CreateVersion7(now));
        await _outbox.EnqueueAsync(
            OutboxCommand.StopCharging(charging.SessionId, stop), now, cancellationToken);
        _dispatcher.Nudge();
        await RefreshAsync(cancellationToken);
    }
}
