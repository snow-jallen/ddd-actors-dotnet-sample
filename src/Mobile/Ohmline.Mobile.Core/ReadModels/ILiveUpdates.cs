using Ohmline.Charging.Client.Contracts.Notifications;

namespace Ohmline.Mobile.Core.ReadModels;

/// <summary>
/// The server's live updates for the driver's token, already applied to the cache: the
/// screens hear that something changed and read the cache again. Runs while the app is in
/// the foreground.
/// </summary>
public interface ILiveUpdates
{
    /// <summary>A card in the cache changed, from a push or a catch-up; on the UI thread.</summary>
    event Action? CardsChanged;

    /// <summary>A session's summary was pushed; on the UI thread.</summary>
    event Action<SessionSummaryChanged>? SummaryChanged;

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}
