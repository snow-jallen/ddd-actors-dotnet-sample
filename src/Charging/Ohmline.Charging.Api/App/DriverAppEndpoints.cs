using System.Data;
using System.Text.Json;
using Akka.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Npgsql;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.App;

/// <summary>
/// The driver app's API (section 14.6), under <c>/app/v1</c>: endpoints shaped for the app's
/// screens, answered from the driver app's read model, never from an aggregate. The prefix is
/// durable: apps in drivers' pockets call it for years. From section 19.2 it speaks only the
/// published language of <c>Ohmline.Charging.Client.Contracts</c>; its commands are in
/// <see cref="DriverAppCommands"/>.
/// </summary>
public static class DriverAppEndpoints
{
    private const int RecentCards = 10;

    private const int DefaultLimit = 100;

    private const int MaxLimit = 500;

    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>The reads from the driver app's read model: only with PostgreSQL.</summary>
        public IEndpointRouteBuilder MapDriverAppEndpoints()
        {
            var tokens = endpoints.MapGroup("/app/v1/tokens/{uid}").WithTags("Driver app");
            tokens.MapGet("/cards", GetCardsAsync);
            tokens.MapGet("/feed", GetFeedAsync);
            return endpoints;
        }

        /// <summary>
        /// The app's commands, and the listing it shows, which the listing actor answers: on
        /// every node, like the rest of Charging's edge.
        /// </summary>
        public IEndpointRouteBuilder MapDriverAppCommandEndpoints()
        {
            var app = endpoints.MapGroup("/app/v1").WithTags("Driver app");
            app.MapPost("/journeys", DriverAppCommands.StartChargingAsync);
            app.MapPost("/sessions/{id:guid}/stop", DriverAppCommands.StopChargingAsync);
            app.MapPost(
                "/charge-points/{id}/connectors/{number:int}/reservations",
                DriverAppCommands.ReserveConnectorAsync);
            app.MapGet("/charge-points/{id}/listing", GetListingAsync);
            return endpoints;
        }
    }

    /// <summary>
    /// <c>GET /app/v1/charge-points/{id}/listing</c>: the listing as drivers see it, from the
    /// listing itself, which is never behind.
    /// </summary>
    public static async Task<Results<Ok<ChargePointListingDto>, ProblemHttpResult>>
        GetListingAsync(
            string id,
            IRequiredActor<ChargePointListingRegion> listings,
            IOptions<ChargingApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (!ChargePointId.TryCreate(id).TryGet(out var chargePointId))
        {
            return Problems.NotFound($"'{id}' is not a charge point id.");
        }

        var timeout = options.Value.AskTimeout;
        var view = await Edge.AskAsync<ChargePointListingView>(
            listings.ActorRef,
            new GetChargePointListing(chargePointId),
            timeout,
            cancellationToken);
        return view switch
        {
            null => Problems.Timeout(timeout),
            { Revision: > 0 } => TypedResults.Ok(view.ToClient()),
            _ => Problems.NotFound($"Charge point {chargePointId} has no listing yet."),
        };
    }

    /// <summary>
    /// <c>GET /app/v1/tokens/{uid}/cards</c>: the token's active session and its recent ones,
    /// with the feed position they are as of, read in one snapshot.
    /// </summary>
    public static async Task<Results<Ok<DriverSessionCards>, ProblemHttpResult>> GetCardsAsync(
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
        return TypedResults.Ok(new DriverSessionCards(
            active,
            [.. cards.Where(card => card != active).Take(RecentCards)],
            position));
    }

    /// <summary>
    /// <c>GET /app/v1/tokens/{uid}/feed?after={position}&amp;limit={n}</c>: the token's card
    /// changes after a position, oldest first, so an app that was away catches up without
    /// reading every card again.
    /// </summary>
    public static async Task<Results<Ok<DriverFeedPage>, ProblemHttpResult>> GetFeedAsync(
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
        var entries = new List<DriverFeedEntry>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new DriverFeedEntry(
                reader.GetInt64(0),
                JsonSerializer.Deserialize(
                    reader.GetString(1), ChargingClientJsonContext.Default.DriverSessionCard)!));
        }

        return TypedResults.Ok(
            new DriverFeedPage(entries, entries is [.., var last] ? last.Position : from));
    }

    private static async Task<List<DriverSessionCard>> NewestCardsAsync(
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
        var cards = new List<DriverSessionCard>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cards.Add(new DriverSessionCard(
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
