using Ohmline.Charging.Client.Contracts.Notifications;
using Ohmline.Mobile.Core.ReadModels;

namespace Ohmline.Mobile.Core.Tests.ReadModels;

/// <summary>The hub as the screens' tests play it: the test says when the cache changed.</summary>
internal sealed class FakeLiveUpdates : ILiveUpdates
{
    public event Action? CardsChanged;

    public event Action<SessionSummaryChanged>? SummaryChanged;

    public void ChangeCards() => CardsChanged?.Invoke();

    public void Summarize(SessionSummaryChanged summary) => SummaryChanged?.Invoke(summary);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;
}
