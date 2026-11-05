using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Views;

/// <summary>
/// A screen's state, one template per case (section 19.5). The switch is over the union, so a
/// new case of <see cref="ScreenState"/> does not compile until it has a template here: CS8509
/// is an error in the app as in the server.
/// </summary>
/// <remarks>
/// The templates are set in XAML. They are not <c>required</c>: MAUI's XAML source generator
/// does not support required or init-only properties (MAUIX2005).
/// </remarks>
public sealed class ScreenStateTemplates : IStateTemplates
{
    public DataTemplate Loading { get; set; } = null!;

    public DataTemplate Ready { get; set; } = null!;

    public DataTemplate Offline { get; set; } = null!;

    public DataTemplate Failed { get; set; } = null!;

    public (DataTemplate Template, object Case) Select(object state) => (ScreenState)state switch
    {
        Loading loading => (Loading, loading),
        Ready ready => (Ready, ready),
        Offline offline => (Offline, offline),
        Failed failed => (Failed, failed),
        null => throw new InvalidOperationException("The screen state is default."),
    };
}
