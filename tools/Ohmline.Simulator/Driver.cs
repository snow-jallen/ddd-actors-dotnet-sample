using System.Net;
using System.Net.Http.Json;

namespace Ohmline.Simulator;

/// <summary>
/// The people around a simulated charger, over Charging's HTTP API: the operator who registers
/// the charge point, its site, a token and the tariff, and the driver who starts a journey and
/// waits for it to be settled.
/// </summary>
public sealed class Driver(HttpClient node)
{
    private const int Attempts = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public async Task SetUpAsync(
        string chargePointId, string siteId, string tokenUid, CancellationToken cancellationToken)
    {
        await PostAsync("/tariffs", new
        {
            tariffId = "slc-standard",
            currency = "USD",
            timeZoneId = "America/Denver",
            components = new[] { new { kind = "energy-rate", price = 0.42m } },
        }, cancellationToken);
        await PostAsync("/charge-points", new
        {
            chargePointId, siteId, connectorMaxPowersKw = new[] { 22m },
        }, cancellationToken);
        await PostAsync("/sites", new
        {
            siteId,
            gridLimitKw = 200m,
            safetyMarginKw = 10m,
            timeZoneId = "America/Denver",
            connectors = new[]
            {
                new
                {
                    connectorId = $"{chargePointId}/1",
                    minPowerKw = 5m,
                    maxPowerKw = 22m,
                    priority = "public",
                },
            },
        }, cancellationToken);
        await PostAsync("/tokens", new
        {
            tokenUid, driver = $"drv-sim{tokenUid[..6].ToLowerInvariant()}",
        }, cancellationToken);
    }

    /// <summary>The driver taps "start": the journey's id, which names its session.</summary>
    public async Task<Guid> StartJourneyAsync(
        string tokenUid, string connectorId, CancellationToken cancellationToken)
    {
        var started = await PostAsync(
            "/journeys", new { tokenUid, connectorId }, cancellationToken);
        return Guid.Parse(started.Headers.Location!.ToString()["/journeys/".Length..]);
    }

    /// <summary>Asks the journey where it is until it gets there, or gives up.</summary>
    public async Task<string> WaitForStepAsync(
        Guid journey, string step, TimeSpan patience, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + patience;
        string? current = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            current = (await node.GetFromJsonAsync<JourneyAnswer>(
                $"/journeys/{journey}", cancellationToken))?.Step;
            if (current == step || current == "abandoned")
            {
                return current;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return current ?? "unknown";
    }

    /// <summary>
    /// Posts a command. A 504 says the command may still have been carried out, so the driver
    /// sends it again with the same Idempotency-Key, as the problem's detail asks: a node that
    /// has just started can take longer than its ask timeout over its first commands.
    /// </summary>
    private async Task<HttpResponseMessage> PostAsync(
        string path, object body, CancellationToken cancellationToken)
    {
        var idempotencyKey = Guid.CreateVersion7().ToString();
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            var answer = await node.SendAsync(request, cancellationToken);
            if (answer.StatusCode is not HttpStatusCode.GatewayTimeout || attempt == Attempts)
            {
                answer.EnsureSuccessStatusCode();
                return answer;
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }
    }

    private sealed record JourneyAnswer(string Step);
}
