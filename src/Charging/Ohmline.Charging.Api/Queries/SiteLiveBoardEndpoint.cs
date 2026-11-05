using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// <c>GET /sites/{id}/live-board</c> (section 13.8): one query of one table, and no actor. It
/// answers in the time a read takes however many charge points the site has, and it may be
/// behind the aggregates by the projection's lag.
/// </summary>
public static class SiteLiveBoardEndpoint
{
    public static async Task<Results<Ok<SiteLiveBoardResponse>, ProblemHttpResult>> GetAsync(
        string id, [FromServices] NpgsqlDataSource readModels, CancellationToken cancellationToken)
    {
        if (!SiteId.TryCreate(id).TryGet(out var siteId))
        {
            return Problems.NotFound($"'{id}' is not a site id.");
        }

        await using var select = readModels.CreateCommand("""
            SELECT connector_id, status, session_id, allocated_kw
            FROM read_models.site_live_board
            WHERE site_id = @site_id
            ORDER BY connector_id
            """);
        select.Parameters.AddWithValue("site_id", siteId.Value);
        var connectors = new List<ConnectorOnBoardResponse>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            connectors.Add(new ConnectorOnBoardResponse(
                ConnectorId: reader.GetString(0),
                Status: reader.GetString(1),
                SessionId: reader.IsDBNull(2) ? null : reader.GetGuid(2),
                AllocatedKw: reader.IsDBNull(3) ? null : reader.GetDecimal(3)));
        }

        return connectors.Count == 0
            ? Problems.NotFound($"The live board has no connectors at site {siteId} yet.")
            : TypedResults.Ok(new SiteLiveBoardResponse(siteId.Value, connectors));
    }
}
