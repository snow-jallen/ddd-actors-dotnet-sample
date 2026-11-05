using System.Net.Http.Json;

namespace Ohmline.SiteEnergy.Acl.GridOperator.Northgate;

/// <summary>Northgate's feed, as the poller needs it: everything after a sequence number.</summary>
public interface INorthgateFeed
{
    Task<NorthgateSignals> SinceAsync(long sequence, CancellationToken cancellationToken);
}

/// <summary><c>GET /signals?since=</c> over HTTP: a typed client, the host's resilience.</summary>
public sealed class NorthgateClient(HttpClient http) : INorthgateFeed
{
    public async Task<NorthgateSignals> SinceAsync(
        long sequence, CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync<NorthgateSignals>(
            $"/signals?since={sequence}", cancellationToken)
        ?? throw new HttpRequestException("Northgate answered with nothing.");
}
