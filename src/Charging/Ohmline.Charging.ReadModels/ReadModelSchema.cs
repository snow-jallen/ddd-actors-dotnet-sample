using Npgsql;

namespace Ohmline.Charging.ReadModels;

/// <summary>
/// Creates the <c>read_models</c> schema and its tables if they are missing. A stopgap: Chapter
/// 20 hands the schema to a migrator that runs before any node starts.
/// </summary>
public static class ReadModelSchema
{
    private const string OneAtATime =
        "SELECT pg_advisory_xact_lock(hashtext('ohmline.read_models'))";

    private const string Tables = """
        CREATE SCHEMA IF NOT EXISTS read_models;

        CREATE TABLE IF NOT EXISTS read_models.projection_offsets (
            projection_id text        NOT NULL,
            tag           text        NOT NULL,
            last_offset   bigint      NOT NULL,
            updated_at    timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (projection_id, tag)
        );
        """;

    /// <summary>
    /// Safe to call from several projections and nodes at once: a transaction-scoped advisory
    /// lock lets one caller at a time in, because PostgreSQL's <c>IF NOT EXISTS</c> alone still
    /// fails when two sessions create the same table at the same moment.
    /// </summary>
    public static async Task EnsureAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var exclusive = new NpgsqlCommand(OneAtATime, connection, transaction))
        {
            await exclusive.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var create = new NpgsqlCommand(Tables, connection, transaction))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
