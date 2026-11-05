using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using Ohmline.Charging.Api.App.Dtos;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.App;

/// <summary>
/// The driver app's API (section 14.6), under <c>/app/v1</c>: endpoints shaped for the app's
/// screens, answered from the driver app's read model, never from an aggregate. The prefix is
/// durable: apps in drivers' pockets call it for years.
/// </summary>
public static class DriverAppEndpoints
{
    private const int RecentCards = 10;

    private const int DefaultLimit = 100;

    private const int MaxLimit = 500;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapDriverAppEndpoints()
        {
            var tokens = endpoints.MapGroup("/app/v1/tokens/{uid}").WithTags("Driver app");
            tokens.MapGet("/cards", GetCardsAsync);
            tokens.MapGet("/feed", GetFeedAsync);
            return endpoints;
        }
    }

    /// <summary>
    /// <c>GET /app/v1/tokens/{uid}/cards</c>: the token's active session and its recent ones,
    /// with the feed position they are as of, read in one snapshot.
    /// </summary>
    public static async Task<Results<Ok<DriverAppCardsDto>, ProblemHttpResult>> GetCardsAsync(
        string uid, [FromServices] NpgsqlDataSource readModels, CancellationToken cancellationToken)
    {
        if (!TokenUid.TryCreate(uid).TryGet(out var tokenUid))
        {
            return Problems.NotFound($"'{uid}' is not a token uid.");
        }

        await using var connection = await readModels.OpenConnectionAsync(cancellationToken);
        await using var snapshot = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var cards = await NewestCardsAsync(snapshot, tokenUid, cancellationToken);
        var position = await FeedPositionAsync(snapshot, tokenUid, cancellationToken);
        await snapshot.CommitAsync(cancellationToken);

        var active = cards.FirstOrDefault(card => card.EndedAt is null);
        return TypedResults.Ok(new DriverAppCardsDto(
            active,
            [.. cards.Where(card => card != active).Take(RecentCards)],
            position));
    }

    /// <summary>
    /// <c>GET /app/v1/tokens/{uid}/feed?after={position}&amp;limit={n}</c>: the token's card
    /// changes after a position, oldest first, so an app that was away catches up without
    /// reading every card again.
    /// </summary>
    public static async Task<Results<Ok<DriverAppFeedDto>, ProblemHttpResult>> GetFeedAsync(
        string uid,
        long? after,
        int? limit,
        [FromServices] NpgsqlDataSource readModels,
        CancellationToken cancellationToken)
    {
        if (!TokenUid.TryCreate(uid).TryGet(out var tokenUid))
        {
            return Problems.NotFound($"'{uid}' is not a token uid.");
        }

        var (from, count) = (after ?? 0, limit ?? DefaultLimit);
        if (from < 0 || count is < 1 or > MaxLimit)
        {
            return Problems.Invalid(new Dictionary<string, string[]>
            {
                [from < 0 ? "after" : "limit"] =
                    [from < 0 ? "A position is 0 or more." : $"From 1 to {MaxLimit}."],
            });
        }

        await using var select = readModels.CreateCommand("""
            SELECT position, payload
            FROM read_models.driver_app_feed
            WHERE token_uid = @token_uid AND position > @after
            ORDER BY position
            LIMIT @limit
            """);
        select.Parameters.AddWithValue("token_uid", tokenUid.Value);
        select.Parameters.AddWithValue("after", from);
        select.Parameters.AddWithValue("limit", count);
        var entries = new List<DriverAppFeedEntryDto>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new DriverAppFeedEntryDto(
                reader.GetInt64(0),
                JsonSerializer.Deserialize<DriverAppCardDto>(reader.GetString(1), Web)!));
        }

        return TypedResults.Ok(
            new DriverAppFeedDto(entries, entries is [.., var last] ? last.Position : from));
    }

    private static async Task<List<DriverAppCardDto>> NewestCardsAsync(
        NpgsqlTransaction snapshot, TokenUid tokenUid, CancellationToken cancellationToken)
    {
        await using var select = new NpgsqlCommand(
            """
            SELECT session_id, token_uid, status, connector_id, charge_point_display_name,
                   energy_kwh, started_at, ended_at, version
            FROM read_models.driver_app_cards
            WHERE token_uid = @token_uid
            ORDER BY started_at DESC
            LIMIT @newest
            """,
            snapshot.Connection,
            snapshot);
        select.Parameters.AddWithValue("token_uid", tokenUid.Value);
        select.Parameters.AddWithValue("newest", RecentCards + 1);
        var cards = new List<DriverAppCardDto>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cards.Add(new DriverAppCardDto(
                SessionId: reader.GetGuid(0),
                TokenUid: reader.GetString(1),
                Status: reader.GetString(2),
                ConnectorId: reader.GetString(3),
                ChargePointDisplayName: reader.IsDBNull(4) ? null : reader.GetString(4),
                EnergyKwh: reader.GetDecimal(5),
                StartedAt: reader.GetFieldValue<DateTimeOffset>(6),
                EndedAt: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                Version: reader.GetInt64(8)));
        }

        return cards;
    }

    private static async Task<long> FeedPositionAsync(
        NpgsqlTransaction snapshot, TokenUid tokenUid, CancellationToken cancellationToken)
    {
        await using var select = new NpgsqlCommand(
            """
            SELECT coalesce(max(position), 0)
            FROM read_models.driver_app_feed
            WHERE token_uid = @token_uid
            """,
            snapshot.Connection,
            snapshot);
        select.Parameters.AddWithValue("token_uid", tokenUid.Value);
        return (long)(await select.ExecuteScalarAsync(cancellationToken))!;
    }
}
