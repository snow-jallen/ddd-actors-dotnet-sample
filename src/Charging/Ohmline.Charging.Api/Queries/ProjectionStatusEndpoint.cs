using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// <c>GET /projections/status</c> (section 13.7): every projection stream's stored offset
/// beside the journal's highest ordering. A projection that has stopped, or skips events,
/// is invisible in its read model; here it is a number. From section 14.3 it also measures each
/// stream's lag: from the moment its last event was written to the journal to the commit of
/// the transaction that handled it.
/// </summary>
public static class ProjectionStatusEndpoint
{
    private const string UndefinedTable = "42P01";

    private const string Status = """
        SELECT o.projection_id,
               o.tag,
               o.last_offset,
               (SELECT coalesce(max(j.ordering), 0) FROM journal j),
               (SELECT count(*) FROM tags t
                WHERE t.tag = o.tag AND t.ordering_id > o.last_offset),
               (SELECT count(*) FROM read_models.projection_failures f
                WHERE f.projection_id = o.projection_id),
               o.updated_at,
               o.last_event_at,
               round(extract(epoch FROM o.updated_at - o.last_event_at) * 1000)::bigint
        FROM read_models.projection_offsets o
        ORDER BY o.projection_id, o.tag
        """;

    public static async Task<Ok<IReadOnlyList<ProjectionStatusResponse>>> GetAsync(
        [FromServices] NpgsqlDataSource readModels, CancellationToken cancellationToken)
    {
        var streams = new List<ProjectionStatusResponse>();
        await using var select = readModels.CreateCommand(Status);
        try
        {
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                streams.Add(new ProjectionStatusResponse(
                    ProjectionId: reader.GetString(0),
                    Tag: reader.GetString(1),
                    StoredOffset: reader.GetInt64(2),
                    JournalMaxOrdering: reader.GetInt64(3),
                    EventsBehind: reader.GetInt64(4),
                    Failures: reader.GetInt64(5),
                    UpdatedAt: reader.GetFieldValue<DateTimeOffset>(6),
                    LastEventAt: reader.IsDBNull(7)
                        ? null
                        : reader.GetFieldValue<DateTimeOffset>(7),
                    LagMs: reader.IsDBNull(8) ? null : reader.GetInt64(8)));
            }
        }
        catch (PostgresException missing) when (missing.SqlState == UndefinedTable)
        {
            // No projection has started yet, so there are no tables to report on.
        }

        return TypedResults.Ok<IReadOnlyList<ProjectionStatusResponse>>(streams);
    }
}
