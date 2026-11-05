using Microsoft.Extensions.Logging;
using Ohmline.Mobile.Core.Api;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Listings;
using Ohmline.Mobile.Core.Outbox;
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
        builder.Services.AddSingleton(new DriverToken(settings.TokenUid));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IConnectivity, MauiConnectivity>();
        builder.Services.AddSingleton<IAppDispatcher, MauiDispatcher>();

        // The phone's durable intent: one SQLite file in the app's own data folder, which the
        // operating system keeps across restarts and app upgrades (section 19.6).
        var database = Path.Combine(FileSystem.AppDataDirectory, "ohmline-driver.db3");
        builder.Services.AddSingleton(new SqliteOutbox($"Data Source={database}"));
        builder.Services.AddSingleton<ICommandSender, HttpCommandSender>();
        builder.Services.AddSingleton<OutboxDispatcher>();

        builder.Services.AddTransient<ChargePointListingViewModel>();
        builder.Services.AddTransient<ListingPage>();
        builder.Services.AddSingleton<StartChargingViewModel>();
        builder.Services.AddTransient<StartChargingPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
