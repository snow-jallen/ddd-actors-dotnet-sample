using System.Net;
using System.Net.Http.Json;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Client.Contracts.Requests;
using Ohmline.Mobile.Core.Charging;

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
    /// <c>POST /app/v1/journeys</c>: the server's answer as the app's own status, accepted at
    /// a version or rejected in the driver's words. No answer, or a 5xx, throws
    /// <see cref="HttpRequestException"/>: the command may or may not have been carried out.
    /// </summary>
    public async Task<CommandStatus> StartChargingAsync(
        StartChargingRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/app/v1/journeys", request, Json.StartChargingRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            var accepted = await response.Content.ReadFromJsonAsync(
                Json.CommandAcceptedDto, cancellationToken);
            return new Accepted(accepted!.Version);
        }

        if ((int)response.StatusCode is >= 400 and < 500)
        {
            var problem = await response.Content.ReadFromJsonAsync(
                Json.ProblemDto, cancellationToken);
            return new Rejected(problem?.Code ?? "", RejectionText.For(problem?.Code));
        }

        response.EnsureSuccessStatusCode();
        throw new HttpRequestException($"Unexpected answer {(int)response.StatusCode}.");
    }
}
