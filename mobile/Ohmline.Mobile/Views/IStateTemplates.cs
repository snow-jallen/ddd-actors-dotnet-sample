namespace Ohmline.Mobile.Views;

/// <summary>
/// One template per case of a union, and the case the template binds to: what a
/// <see cref="StateView"/> shows for a state.
/// </summary>
public interface IStateTemplates
{
    (DataTemplate Template, object Case) Select(object state);
}
