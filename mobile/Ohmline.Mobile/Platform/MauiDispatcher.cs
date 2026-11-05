using Ohmline.Mobile.Core.Platform;

namespace Ohmline.Mobile.Platform;

/// <summary>The app's <see cref="IAppDispatcher"/>: MAUI's main thread.</summary>
public sealed class MauiDispatcher : IAppDispatcher
{
    public void Post(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
