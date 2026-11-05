using Ohmline.Mobile.Core.Outbox;

namespace Ohmline.Mobile;

/// <summary>
/// The app and its one window. The window's lifecycle drives the outbox's dispatcher
/// (section 19.6): it runs while the app is in the foreground, and stops when the app goes to
/// the background, where the operating system may kill it at any moment without a word. What
/// it had not sent is still in the outbox when the app comes back, or starts again.
/// </summary>
public sealed partial class App : Application
{
    private readonly OutboxDispatcher _dispatcher;

    public App(OutboxDispatcher dispatcher)
    {
        InitializeComponent();
        _dispatcher = dispatcher;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Created += (_, _) => Resume();
        window.Resumed += (_, _) => Resume();
        window.Stopped += async (_, _) => await _dispatcher.StopAsync();
        return window;
    }

    private void Resume()
    {
        _dispatcher.Start();
        _dispatcher.Nudge();
    }
}
