using Ohmline.Mobile.Core.Platform;

namespace Ohmline.Mobile.Core.Tests;

/// <summary>The UI thread, as a test sees it: the action runs at once.</summary>
internal sealed class InlineDispatcher : IAppDispatcher
{
    public void Post(Action action) => action();
}
