using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.Billing.Tests;

/// <summary>
/// Charging's feed, as far as Billing can tell: the entries it is given, served in pages after
/// the position asked for, the way <c>GET /feeds/charging</c> serves them.
/// </summary>
internal sealed class FeedServer(params ChargingIntegrationEvent[] events) : HttpMessageHandler
{
    private readonly List<ChargingFeedEntry> _entries =
    [
        .. events.Select((@event, index) => new ChargingFeedEntry(
            index + 1, JsonSerializer.SerializeToElement(@event, PublishedLanguage.Json))),
    ];

    public int Requests { get; private set; }

    /// <summary>An entry of a type Billing has never heard of, at the next position.</summary>
    public FeedServer ThenUnknown(string type)
    {
        var json = JsonSerializer.SerializeToElement(new { type, eventId = Guid.NewGuid() });
        _entries.Add(new ChargingFeedEntry(_entries.Count + 1, json));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
        var after = long.Parse(query["after"]!, System.Globalization.CultureInfo.InvariantCulture);
        var limit = int.Parse(query["limit"]!, System.Globalization.CultureInfo.InvariantCulture);
        var entries = _entries.Where(entry => entry.Position > after).Take(limit).ToList();
        var last = entries is [.., var newest] ? newest.Position : after;
        var page = new ChargingFeedPage(
            [.. entries], $"{ChargingFeedPage.Path}?after={last}&limit={limit}");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(page, options: PublishedLanguage.Json),
        });
    }
}
