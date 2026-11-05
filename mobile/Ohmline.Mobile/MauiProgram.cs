using Microsoft.Extensions.Logging;
using Ohmline.Mobile.Core.Api;
using Ohmline.Mobile.Core.Listings;
using Ohmline.Mobile.Core.Platform;
using Ohmline.Mobile.Pages;
using Ohmline.Mobile.Platform;
using Ohmline.Mobile.Settings;
// MAUI's global usings bring its own IConnectivity; the app's port is this one.
using IConnectivity = Ohmline.Mobile.Core.Platform.IConnectivity;

namespace Ohmline.Mobile;

/// <summary>
/// The app's composition root (section 19.4): the core's services, one <see cref="HttpClient"/>
/// for the server named in the embedded <c>appsettings.json</c>, the platform adapters, and the
/// pages. Everything with logic comes from <c>Ohmline.Mobile.Core</c>.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var settings = AppSettings.Load();
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // One client for the app's life: a phone makes few requests, to one server.
        builder.Services.AddSingleton(new HttpClient { BaseAddress = settings.ApiBaseAddress });
        builder.Services.AddSingleton<ChargingApiClient>();
        builder.Services.AddSingleton<IConnectivity, MauiConnectivity>();
        builder.Services.AddSingleton<IAppDispatcher, MauiDispatcher>();
        builder.Services.AddTransient<ChargePointListingViewModel>();
        builder.Services.AddTransient<ListingPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
