using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using Ohmline.Charging.Contracts.Integration;

namespace Ohmline.Charging.Api.Feeds;

/// <summary>
/// Charging's Open Host Service (section 16.2): one feed of its Published Language for every
/// consumer, read from the outbox the relay writes. A notification log in Vernon's sense: the
/// consumer keeps its own position and pulls; Charging keeps no list of who reads, and a new
/// consumer starts from position 0 without asking anyone.
/// </summary>
public static class FeedEndpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapChargingFeed()
        {
            endpoints.MapGet(ChargingFeedPage.Path, GetAsync).WithTags("Feeds");
            return endpoints;
        }
    }

    /// <summary>
    /// <c>GET /feeds/charging?after={position}&amp;limit={n}</c>: up to n integration events
    /// after the position, oldest first, with an <c>ETag</c> and a link to the next page. A
    /// consumer that sends the ETag back gets 304 until the page has changed.
    /// </summary>
    public static async Task<Results<Ok<ChargingFeedPage>, StatusCodeHttpResult, ProblemHttpResult>>
        GetAsync(
            long? after,
            int? limit,
            HttpContext http,
            [FromServices] NpgsqlDataSource readModels,
            CancellationToken cancellationToken)
    {
        var (from, count) = (after ?? 0, limit ?? FeedPaging.DefaultLimit);
        if (from < 0 || count is < 1 or > FeedPaging.MaxLimit)
        {
            return Problems.Invalid(new Dictionary<string, string[]>
            {
                [from < 0 ? "after" : "limit"] =
                    [from < 0 ? "A position is 0 or more." : $"From 1 to {FeedPaging.MaxLimit}."],
            });
        }

        var entries = await EntriesAsync(readModels, from, count, cancellationToken);
        long? last = entries is [.., var newest] ? newest.Position : null;
        var etag = FeedPaging.ETag(from, last);
        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = FeedPaging.CacheControl(entries.Count, count);
        if (http.Request.Headers.IfNoneMatch.Contains(etag))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        return TypedResults.Ok(new ChargingFeedPage(
            [.. entries],
            FeedPaging.NextLink(ChargingFeedPage.Path, from, count, last)));
    }

    private static async Task<List<ChargingFeedEntry>> EntriesAsync(
        NpgsqlDataSource readModels, long after, int limit, CancellationToken cancellationToken)
    {
        await using var select = readModels.CreateCommand("""
            SELECT position, payload
            FROM read_models.integration_outbox
            WHERE position > @after
            ORDER BY position
            LIMIT @limit
            """);
        select.Parameters.AddWithValue("after", after);
        select.Parameters.AddWithValue("limit", limit);
        var entries = new List<ChargingFeedEntry>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            using var payload = JsonDocument.Parse(reader.GetString(1));
            entries.Add(new ChargingFeedEntry(reader.GetInt64(0), payload.RootElement.Clone()));
        }

        return entries;
    }
}
