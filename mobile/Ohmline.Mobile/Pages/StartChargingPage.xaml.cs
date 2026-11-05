using Ohmline.Mobile.Core.Charging;

namespace Ohmline.Mobile.Pages;

/// <summary>Start charging (section 19.5): the view model does the work.</summary>
public sealed partial class StartChargingPage : ContentPage
{
    public StartChargingPage(StartChargingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
