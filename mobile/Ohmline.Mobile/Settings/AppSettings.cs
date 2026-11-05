using System.Text.Json;

namespace Ohmline.Mobile.Settings;

/// <summary>
/// The app's settings, from the <c>Ohmline</c> section of the <c>appsettings.json</c> embedded
/// in the package: a phone has no environment variables from the developer's machine. Read with
/// <see cref="JsonDocument"/>, which needs no reflection, so trimming cannot break it.
/// </summary>
/// <param name="ApiBaseAddress">The server: the development machine's node, in Debug.</param>
/// <param name="TokenUid">The driver's id token in the app; a sign-in would provide it.</param>
/// <param name="OtlpEndpoint">
/// Where to send traces, over OTLP with HTTP and protobuf, such as the Aspire dashboard's
/// <c>/v1/traces</c>; none sends no traces (section 19.8).
/// </param>
public sealed record AppSettings(Uri ApiBaseAddress, string TokenUid, Uri? OtlpEndpoint)
{
    private const string Resource = "appsettings.json";

    public static AppSettings Load()
    {
        using var stream = typeof(AppSettings).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"The app package has no {Resource}.");
        using var json = JsonDocument.Parse(stream);
        var ohmline = json.RootElement.GetProperty("Ohmline");
        return new AppSettings(
            new Uri(ohmline.GetProperty("ApiBaseAddress").GetString()!),
            ohmline.GetProperty("TokenUid").GetString()!,
            ohmline.TryGetProperty("OtlpEndpoint", out var otlp) && otlp.GetString() is { } url
                ? new Uri(url)
                : null);
    }
}
