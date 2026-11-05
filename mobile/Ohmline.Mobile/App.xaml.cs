using Ohmline.Mobile.Core.Outbox;
using Ohmline.Mobile.Core.ReadModels;

namespace Ohmline.Mobile;

/// <summary>
/// The app and its one window. The window's lifecycle drives the outbox's dispatcher
/// (section 19.6) and the hub connection (section 19.7): both run while the app is in the
/// foreground, and stop when the app goes to the background, where the operating system may
/// kill it at any moment without a word. What the dispatcher had not sent is still in the
/// outbox when the app comes back, or starts again; what the hub pushed meanwhile is read
/// from the feed.
/// </summary>
public sealed partial class App : Application
{
    private readonly OutboxDispatcher _dispatcher;
    private readonly ILiveUpdates _live;

    public App(OutboxDispatcher dispatcher, ILiveUpdates live)
    {
        InitializeComponent();
        (_dispatcher, _live) = (dispatcher, live);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Created += (_, _) => Resume();
        window.Resumed += (_, _) => Resume();
        window.Stopped += async (_, _) => await StopAsync();
        return window;
    }

    private void Resume()
    {
        _dispatcher.Start();
        _dispatcher.Nudge();
        // Connects, joins the token's group and catches up; retries until it can.
        _ = _live.StartAsync(CancellationToken.None);
    }

    private async Task StopAsync()
    {
        await _dispatcher.StopAsync();
        await _live.StopAsync();
    }
}
