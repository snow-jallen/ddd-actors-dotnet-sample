namespace Ohmline.Mobile.Core.Platform;

/// <summary>
/// Runs an action on the UI thread: what a view model's change must reach before a page can
/// show it. The MAUI head adapts <c>MainThread</c>; a test runs the action at once.
/// </summary>
public interface IAppDispatcher
{
    void Post(Action action);
}
