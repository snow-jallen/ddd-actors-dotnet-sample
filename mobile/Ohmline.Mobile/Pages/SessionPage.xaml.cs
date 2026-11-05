using Ohmline.Mobile.Core.ReadModels;

namespace Ohmline.Mobile.Pages;

/// <summary>
/// The driver's session card (section 19.7): read from the phone's cache whenever the page
/// appears, and again whenever a push or the outbox changes it.
/// </summary>
public sealed partial class SessionPage : ContentPage
{
    private readonly SessionCardViewModel _viewModel;

    public SessionPage(SessionCardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshCommand.Execute(null);
    }
}
