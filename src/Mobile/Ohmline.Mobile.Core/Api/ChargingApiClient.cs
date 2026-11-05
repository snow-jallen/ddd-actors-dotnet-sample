using System.Net;
using System.Net.Http.Json;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.ReadModels;

namespace Ohmline.Mobile.Core.Api;

/// <summary>
/// Charging's <c>/app/v1</c> API as the app calls it: a typed client over the
/// <see cref="HttpClient"/> the head gives it, whose base address is the server's. Every body is
/// read and written with the source-generated <see cref="ChargingClientJsonContext"/>, never by
/// reflection, so nothing here breaks when the app is trimmed or compiled ahead of time.
/// </summary>
/// <remarks>
/// It speaks the published language and nothing else: what a DTO means on a screen is the
/// view models' business, the app's thin anticorruption layer (section 19.1).
/// </remarks>
public sealed class ChargingApiClient(HttpClient http)
{
    private static readonly ChargingClientJsonContext Json = ChargingClientJsonContext.Default;

    /// <summary>
    /// <c>GET /app/v1/charge-points/{id}/listing</c>: the listing, or null when the charge point
    /// has none. A network failure or a server error throws <see cref="HttpRequestException"/>.
    /// </summary>
    public async Task<ChargePointListingDto?> GetListingAsync(
        string chargePointId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/app/v1/charge-points/{Uri.EscapeDataString(chargePointId)}/listing",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            Json.ChargePointListingDto, cancellationToken);
    }

    /// <summary>
    /// <c>GET /app/v1/tokens/{uid}/feed?after={position}</c>: the token's card changes after a
    /// position, oldest first, a page at a time.
    /// </summary>
    public async Task<DriverFeedPage> GetFeedAsync(
        string tokenUid, long after, CancellationToken cancellationToken) =>
        (await http.GetFromJsonAsync(
            $"/app/v1/tokens/{Uri.EscapeDataString(tokenUid)}/feed?after={after}",
            Json.DriverFeedPage,
            cancellationToken))!;
}
