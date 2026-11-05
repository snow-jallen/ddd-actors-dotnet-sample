using Microsoft.Data.Sqlite;

namespace Ohmline.Charging.Actors.Tests.JournalTour;

/// <summary>
/// The SELECT statements of the journal tour (section 10.8), for reading Akka.Persistence.Sql's
/// SQLite tables by hand. For learning and for incident work only: application code never reads
/// these tables, it asks the actors or, from Chapter 13, the read journal.
/// </summary>
public static class JournalTourQueries
{
    /// <summary>The tables and indexes, as autoInitialize created them.</summary>
    public const string Schema = """
        SELECT name, sql FROM sqlite_master
        WHERE type IN ('table', 'index') AND sql IS NOT NULL AND name <> 'sqlite_sequence'
        ORDER BY tbl_name, type DESC, name
        """;

    /// <summary>One aggregate's events in order. The message column is UTF-8 JSON.</summary>
    public const string Journal = """
        SELECT ordering, persistence_id, sequence_number, created, deleted,
               manifest, identifier, writer_uuid, message
        FROM journal
        WHERE persistence_id = $persistenceId
        ORDER BY sequence_number
        """;

    /// <summary>The tags of one aggregate's events, one row per event and tag.</summary>
    public const string Tags = """
        SELECT ordering_id, tag, persistence_id, sequence_nr
        FROM tags
        WHERE persistence_id = $persistenceId
        ORDER BY ordering_id, tag
        """;

    /// <summary>One aggregate's snapshots. The snapshot column is UTF-8 JSON.</summary>
    public const string Snapshots = """
        SELECT persistence_id, sequence_number, created, manifest, serializer_id, snapshot
        FROM snapshot
        WHERE persistence_id = $persistenceId
        ORDER BY sequence_number
        """;

    public static async Task<IReadOnlyList<JournalRow>> ReadJournalAsync(
        SqliteConnection connection, string persistenceId, CancellationToken cancellationToken)
    {
        var rows = new List<JournalRow>();
        await using var reader = await QueryAsync(
            connection, Journal, persistenceId, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new JournalRow(
                Ordering: reader.GetInt64(0),
                PersistenceId: reader.GetString(1),
                SequenceNumber: reader.GetInt64(2),
                Created: reader.GetInt64(3),
                Deleted: reader.GetBoolean(4),
                Manifest: reader.GetString(5),
                Identifier: reader.GetInt32(6),
                WriterUuid: reader.GetString(7),
                Message: (byte[])reader.GetValue(8)));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<TagRow>> ReadTagsAsync(
        SqliteConnection connection, string persistenceId, CancellationToken cancellationToken)
    {
        var rows = new List<TagRow>();
        await using var reader = await QueryAsync(
            connection, Tags, persistenceId, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new TagRow(
                OrderingId: reader.GetInt64(0),
                Tag: reader.GetString(1),
                PersistenceId: reader.GetString(2),
                SequenceNr: reader.GetInt64(3)));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<SnapshotRow>> ReadSnapshotsAsync(
        SqliteConnection connection, string persistenceId, CancellationToken cancellationToken)
    {
        var rows = new List<SnapshotRow>();
        await using var reader = await QueryAsync(
            connection, Snapshots, persistenceId, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SnapshotRow(
                PersistenceId: reader.GetString(0),
                SequenceNumber: reader.GetInt64(1),
                Created: reader.GetInt64(2),
                Manifest: reader.GetString(3),
                SerializerId: reader.GetInt32(4),
                Snapshot: (byte[])reader.GetValue(5)));
        }

        return rows;
    }

    /// <summary>Any query above, each row as one line of <c>column=value</c> pairs.</summary>
    public static async Task<IReadOnlyList<string>> ReadLinesAsync(
        SqliteConnection connection,
        string query,
        string persistenceId,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        await using var reader = await QueryAsync(
            connection, query, persistenceId, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(string.Join(" | ", Enumerable.Range(0, reader.FieldCount)
                .Select(column => $"{reader.GetName(column)}={reader.GetValue(column)}")));
        }

        return lines;
    }

    private static async Task<SqliteDataReader> QueryAsync(
        SqliteConnection connection,
        string query,
        string persistenceId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("$persistenceId", persistenceId);
        return await command.ExecuteReaderAsync(cancellationToken);
    }
}
