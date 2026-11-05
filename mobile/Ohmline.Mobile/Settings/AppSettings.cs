using System.Text.Json;

namespace Ohmline.Mobile.Settings;

/// <summary>
/// The app's settings, from the <c>Ohmline</c> section of the <c>appsettings.json</c> embedded
/// in the package: a phone has no environment variables from the developer's machine. Read with
/// <see cref="JsonDocument"/>, which needs no reflection, so trimming cannot break it.
/// </summary>
public sealed record AppSettings(Uri ApiBaseAddress)
{
    private const string Resource = "appsettings.json";

    public static AppSettings Load()
    {
        using var stream = typeof(AppSettings).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"The app package has no {Resource}.");
        using var json = JsonDocument.Parse(stream);
        var ohmline = json.RootElement.GetProperty("Ohmline");
        return new AppSettings(new Uri(ohmline.GetProperty("ApiBaseAddress").GetString()!));
    }
}
