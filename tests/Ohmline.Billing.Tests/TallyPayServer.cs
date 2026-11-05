using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Ohmline.Billing.Tests;

/// <summary>
/// TallyPay as Billing's tests need it: it approves every call, refuses cards whose token says
/// <c>declined</c>, fails with 503 as often as it is told to, and remembers every request.
/// </summary>
internal sealed class TallyPayServer : HttpMessageHandler
{
    private int _failures;

    public ConcurrentQueue<(string Path, string? Key, JsonObject Body)> Requests { get; } = new();

    public IEnumerable<string> Paths => Requests.Select(request => request.Path);

    /// <summary>The next <paramref name="times"/> requests answer 503; -1 for all.</summary>
    public TallyPayServer Failing(int times = -1)
    {
        _failures = times;
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = JsonNode.Parse(
            await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
        var key = request.Headers.TryGetValues("X-Idempotency-Key", out var keys)
            ? keys.Single()
            : null;
        Requests.Enqueue((request.RequestUri!.AbsolutePath, key, body));
        if (_failures != 0)
        {
            _failures = _failures > 0 ? _failures - 1 : _failures;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        var declined = body["CARD_TOKEN"]?.GetValue<string>().Contains("declined") == true;
        var reply = new Dictionary<string, string>
        {
            ["STATUS_CD"] = declined ? "05" : "00",
            ["AUTH_ID"] = body["AUTH_ID"]?.GetValue<string>() ?? $"TP{key?.GetHashCode():X8}",
            ["TXN_DT"] = "20261104",
            ["MSG"] = declined ? "DO NOT HONOUR" : "APPROVED",
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(reply) };
    }
}
