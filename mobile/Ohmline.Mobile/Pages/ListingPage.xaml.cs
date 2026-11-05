using Ohmline.Mobile.Core.Listings;

namespace Ohmline.Mobile.Pages;

/// <summary>A charge point's listing (section 19.4): the view model does the work.</summary>
public sealed partial class ListingPage : ContentPage
{
    public ListingPage(ChargePointListingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
