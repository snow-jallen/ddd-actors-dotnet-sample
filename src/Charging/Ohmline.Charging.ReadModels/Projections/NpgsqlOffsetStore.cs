using Npgsql;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// Offsets in <c>read_models.projection_offsets</c>, one row per projection stream, in the same
/// database as the read models they describe.
/// </summary>
public sealed class NpgsqlOffsetStore(NpgsqlDataSource dataSource) : IOffsetStore
{
    /// <summary>
    /// Also writes the stream down at offset 0 the first time it starts, so a stream that is
    /// stuck on its very first event shows up in <c>GET /projections/status</c> (section 13.7).
    /// </summary>
    public async Task<long> LoadAsync(
        ProjectionStream stream, CancellationToken cancellationToken)
    {
        await using (var register = dataSource.CreateCommand("""
            INSERT INTO read_models.projection_offsets (projection_id, tag, last_offset)
            VALUES (@projection_id, @tag, 0)
            ON CONFLICT (projection_id, tag) DO NOTHING
            """))
        {
            register.Parameters.AddWithValue("projection_id", stream.ProjectionId);
            register.Parameters.AddWithValue("tag", stream.Tag);
            await register.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = dataSource.CreateCommand("""
            SELECT last_offset
            FROM read_models.projection_offsets
            WHERE projection_id = @projection_id AND tag = @tag
            """);
        command.Parameters.AddWithValue("projection_id", stream.ProjectionId);
        command.Parameters.AddWithValue("tag", stream.Tag);
        return await command.ExecuteScalarAsync(cancellationToken) is long offset ? offset : 0;
    }

    /// <summary>
    /// <c>updated_at</c> is <c>clock_timestamp()</c>, the time of this statement, the last
    /// before the commit: <c>now()</c> would be the time the transaction began.
    /// </summary>
    public async Task SaveAsync(
        ProjectionStream stream,
        long offset,
        DateTimeOffset eventWrittenAt,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO read_models.projection_offsets
                (projection_id, tag, last_offset, last_event_at, updated_at)
            VALUES (@projection_id, @tag, @last_offset, @last_event_at, clock_timestamp())
            ON CONFLICT (projection_id, tag) DO UPDATE SET
                last_offset = excluded.last_offset,
                last_event_at = excluded.last_event_at,
                updated_at = excluded.updated_at
            """,
            transaction.Connection,
            transaction);
        command.Parameters.AddWithValue("projection_id", stream.ProjectionId);
        command.Parameters.AddWithValue("tag", stream.Tag);
        command.Parameters.AddWithValue("last_offset", offset);
        command.Parameters.AddWithValue("last_event_at", eventWrittenAt.ToUniversalTime());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
