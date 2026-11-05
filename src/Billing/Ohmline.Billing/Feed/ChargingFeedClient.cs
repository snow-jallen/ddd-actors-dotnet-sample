using System.Net.Http.Json;
using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.Billing.Feed;

/// <summary>The charging feed over HTTP: a typed client with the host's resilience.</summary>
public sealed class ChargingFeedClient(HttpClient http)
{
    public async Task<ChargingFeedPage> PageAfterAsync(
        long position, int limit, CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync<ChargingFeedPage>(
            $"{ChargingFeedPage.Path}?after={position}&limit={limit}",
            PublishedLanguage.Json,
            cancellationToken)
        ?? throw new HttpRequestException("The charging feed answered with no page.");
}
