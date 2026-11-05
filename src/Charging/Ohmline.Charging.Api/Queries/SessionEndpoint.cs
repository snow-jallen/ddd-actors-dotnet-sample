using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// <c>GET /sessions/{id}</c> (section 14.1): one session from the session history. With
/// <c>?minVersion=n</c>, the version a command just returned, it reads your own write: 200 once
/// the history has caught up with version n, or 202 with <c>Retry-After</c> and the version it
/// has when it has not caught up in time.
/// </summary>
public static class SessionEndpoint
{
    public static async Task<
            Results<Ok<SessionResponse>, Accepted<ReadModelBehindResponse>, ProblemHttpResult>>
        GetAsync(
            Guid id,
            long? minVersion,
            [FromServices] NpgsqlDataSource readModels,
            [FromServices] ReadYourWrites readYourWrites,
            HttpResponse response,
            CancellationToken cancellationToken)
    {
        if (!SessionId.TryCreate(id).TryGet(out var sessionId))
        {
            return Problems.NotFound($"'{id}' is not a session id.");
        }

        if (minVersion is not { } atLeast)
        {
            return await FindAsync(readModels, sessionId, cancellationToken) is { } row
                ? Current(row, response)
                : Problems.NotFound($"The session history has no session {sessionId} yet.");
        }

        if (atLeast < 1)
        {
            return Problems.Invalid(new Dictionary<string, string[]>
            {
                ["minVersion"] = ["A version starts at 1."],
            });
        }

        var read = await readYourWrites.ReadAsync(
            token => FindAsync(readModels, sessionId, token),
            row => row.Version,
            atLeast,
            cancellationToken);
        return read switch
        {
            CaughtUp<SessionResponse> caughtUp => Current(caughtUp.Row, response),
            StillBehind behind => NotYet(sessionId, behind.Version, atLeast, response),
            null => throw new InvalidOperationException("A read found neither answer."),
        };
    }

    /// <summary>The row, with its version as the entity tag.</summary>
    private static Ok<SessionResponse> Current(SessionResponse row, HttpResponse response)
    {
        response.Headers.ETag = $"\"{row.Version}\"";
        return TypedResults.Ok(row);
    }

    /// <summary>Not yet: ask the same question again in a second.</summary>
    private static Accepted<ReadModelBehindResponse> NotYet(
        SessionId sessionId, long version, long minVersion, HttpResponse response)
    {
        response.Headers.RetryAfter = "1";
        return TypedResults.Accepted(
            SessionEndpoints.SessionLocation(sessionId, minVersion),
            new ReadModelBehindResponse(version, minVersion));
    }

    /// <summary>The session's row, or null while the history has none.</summary>
    internal static async Task<SessionResponse?> FindAsync(
        NpgsqlDataSource readModels, SessionId sessionId, CancellationToken cancellationToken)
    {
        await using var select = readModels.CreateCommand("""
            SELECT session_id, token_uid, connector_id, status, energy_kwh, started_at, ended_at,
                   last_sequence_nr
            FROM read_models.session_history
            WHERE session_id = @session_id
            """);
        select.Parameters.AddWithValue("session_id", sessionId.Value);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SessionResponse(
            SessionId: reader.GetGuid(0),
            TokenUid: reader.GetString(1),
            ConnectorId: reader.GetString(2),
            Status: reader.GetString(3),
            EnergyKwh: reader.GetDecimal(4),
            StartedAt: reader.GetFieldValue<DateTimeOffset>(5),
            EndedAt: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            Version: reader.GetInt64(7));
    }
}
