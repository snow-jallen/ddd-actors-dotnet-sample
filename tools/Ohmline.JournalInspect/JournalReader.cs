using System.Data.Common;

namespace Ohmline.JournalInspect;

/// <summary>
/// Reads the journal's tables over any ADO.NET connection: a <c>NpgsqlConnection</c> for the
/// cluster's PostgreSQL, a <c>SqliteConnection</c> for the development node's file.
/// </summary>
public sealed class JournalReader(DbConnection connection)
{
    /// <summary>One aggregate's events, by its persistence id, in sequence order.</summary>
    public Task<IReadOnlyList<StoredRow>> EventsOfAsync(
        string persistenceId, CancellationToken cancellationToken) =>
        ReadAsync(
            JournalQueries.Journal, EventRow, cancellationToken, ("@persistenceId", persistenceId));

    /// <summary>Up to <paramref name="limit"/> events of any aggregate after a position.</summary>
    public Task<IReadOnlyList<StoredRow>> EventsAfterAsync(
        long after, int limit, CancellationToken cancellationToken) =>
        ReadAsync(
            JournalQueries.Recent,
            EventRow,
            cancellationToken,
            ("@after", after),
            ("@limit", limit));

    /// <summary>One aggregate's snapshots, oldest first.</summary>
    public Task<IReadOnlyList<StoredRow>> SnapshotsOfAsync(
        string persistenceId, CancellationToken cancellationToken) =>
        ReadAsync(
            JournalQueries.Snapshots,
            SnapshotRow,
            cancellationToken,
            ("@persistenceId", persistenceId));

    private static StoredRow EventRow(DbDataReader row) => new(
        Ordering: row.GetInt64(0),
        PersistenceId: row.GetString(1),
        SequenceNr: row.GetInt64(2),
        Created: row.GetInt64(3),
        SerializerId: row.IsDBNull(6) ? null : row.GetInt32(6),
        Manifest: row.IsDBNull(5) ? null : row.GetString(5),
        Payload: (byte[])row.GetValue(8));

    private static StoredRow SnapshotRow(DbDataReader row) => new(
        Ordering: null,
        PersistenceId: row.GetString(0),
        SequenceNr: row.GetInt64(1),
        Created: row.GetInt64(2),
        SerializerId: row.IsDBNull(4) ? null : row.GetInt32(4),
        Manifest: row.IsDBNull(3) ? null : row.GetString(3),
        Payload: row.IsDBNull(5) ? [] : (byte[])row.GetValue(5));

    private async Task<IReadOnlyList<StoredRow>> ReadAsync(
        string query,
        Func<DbDataReader, StoredRow> map,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        var rows = new List<StoredRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }
}
