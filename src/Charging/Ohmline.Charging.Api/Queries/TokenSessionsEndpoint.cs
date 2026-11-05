using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// <c>GET /tokens/{uid}/sessions</c> (section 13.8): a driver token's sessions, newest first,
/// from the session history. Asking every session aggregate instead would wake each one, and
/// nothing could even list them: the journal has no index by token.
/// </summary>
public static class TokenSessionsEndpoint
{
    private const int Newest = 50;

    public static async Task<Results<Ok<IReadOnlyList<TokenSessionResponse>>, ProblemHttpResult>>
        GetAsync(
            string uid,
            [FromServices] NpgsqlDataSource readModels,
            CancellationToken cancellationToken)
    {
        if (!TokenUid.TryCreate(uid).TryGet(out var tokenUid))
        {
            return Problems.NotFound($"'{uid}' is not a token uid.");
        }

        await using var select = readModels.CreateCommand("""
            SELECT session_id, connector_id, status, energy_kwh, started_at, ended_at,
                   last_sequence_nr
            FROM read_models.session_history
            WHERE token_uid = @token_uid
            ORDER BY started_at DESC
            LIMIT @newest
            """);
        select.Parameters.AddWithValue("token_uid", tokenUid.Value);
        select.Parameters.AddWithValue("newest", Newest);
        var sessions = new List<TokenSessionResponse>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(new TokenSessionResponse(
                SessionId: reader.GetGuid(0),
                ConnectorId: reader.GetString(1),
                Status: reader.GetString(2),
                EnergyKwh: reader.GetDecimal(3),
                StartedAt: reader.GetFieldValue<DateTimeOffset>(4),
                EndedAt: reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                Version: reader.GetInt64(6)));
        }

        return TypedResults.Ok<IReadOnlyList<TokenSessionResponse>>(sessions);
    }
}
