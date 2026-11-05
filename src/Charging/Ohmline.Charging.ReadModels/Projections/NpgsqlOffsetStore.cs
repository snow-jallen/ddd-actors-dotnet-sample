using Npgsql;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// Offsets in <c>read_models.projection_offsets</c>, one row per projection stream, in the same
/// database as the read models they describe.
/// </summary>
public sealed class NpgsqlOffsetStore(NpgsqlDataSource dataSource) : IOffsetStore
{
    public async Task<long> LoadAsync(
        ProjectionStream stream, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT last_offset
            FROM read_models.projection_offsets
            WHERE projection_id = @projection_id AND tag = @tag
            """);
        command.Parameters.AddWithValue("projection_id", stream.ProjectionId);
        command.Parameters.AddWithValue("tag", stream.Tag);
        return await command.ExecuteScalarAsync(cancellationToken) is long offset ? offset : 0;
    }

    public async Task SaveAsync(
        ProjectionStream stream,
        long offset,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO read_models.projection_offsets (projection_id, tag, last_offset)
            VALUES (@projection_id, @tag, @last_offset)
            ON CONFLICT (projection_id, tag)
            DO UPDATE SET last_offset = excluded.last_offset, updated_at = now()
            """,
            transaction.Connection,
            transaction);
        command.Parameters.AddWithValue("projection_id", stream.ProjectionId);
        command.Parameters.AddWithValue("tag", stream.Tag);
        command.Parameters.AddWithValue("last_offset", offset);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
