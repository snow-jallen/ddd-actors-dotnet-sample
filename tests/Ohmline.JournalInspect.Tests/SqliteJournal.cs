using Microsoft.Data.Sqlite;

namespace Ohmline.JournalInspect.Tests;

/// <summary>
/// A temporary SQLite file with the journal and snapshot tables as Akka.Persistence.Sql creates
/// them (<c>docs/journal-tour.md</c>), to which a test adds rows by hand. Deleted on dispose.
/// </summary>
internal sealed class SqliteJournal : IAsyncDisposable
{
    private const string Tables = """
        CREATE TABLE journal (
            ordering        INTEGER       NOT NULL PRIMARY KEY AUTOINCREMENT,
            created         INTEGER       NOT NULL,
            deleted         Bit           NOT NULL,
            persistence_id  NVarChar(255) NOT NULL,
            sequence_number INTEGER       NOT NULL,
            message         VarBinary     NOT NULL,
            manifest        NVarChar(500)     NULL,
            identifier      INTEGER           NULL,
            writer_uuid     NVarChar(128)     NULL
        );
        CREATE TABLE snapshot (
            persistence_id  NVarChar(255) NOT NULL,
            sequence_number INTEGER       NOT NULL,
            created         INTEGER       NOT NULL,
            snapshot        VarBinary         NULL,
            manifest        NVarChar(500)     NULL,
            serializer_id   INTEGER           NULL,
            PRIMARY KEY (persistence_id, sequence_number)
        );
        """;

    private SqliteJournal(string path) => Path = path;

    public string Path { get; }

    public string ConnectionString => $"Data Source={Path}";

    public static async Task<SqliteJournal> CreateAsync(CancellationToken cancellationToken)
    {
        var journal = new SqliteJournal(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"ohmline-inspect-{Guid.NewGuid():N}.db"));
        await journal.ExecuteAsync(Tables, [], cancellationToken);
        return journal;
    }

    public Task AddEventAsync(
        string persistenceId,
        long sequenceNr,
        (int SerializerId, string Manifest, byte[] Bytes) payload,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            """
            INSERT INTO journal (created, deleted, persistence_id, sequence_number, message,
                                 manifest, identifier, writer_uuid)
            VALUES ($created, 0, $persistenceId, $sequenceNr, $message, $manifest, $identifier,
                    'by-hand')
            """,
            [
                ("$created", Payloads.At.UtcTicks),
                ("$persistenceId", persistenceId),
                ("$sequenceNr", sequenceNr),
                ("$message", payload.Bytes),
                ("$manifest", payload.Manifest),
                ("$identifier", payload.SerializerId),
            ],
            cancellationToken);

    public Task AddSnapshotAsync(
        string persistenceId,
        long sequenceNr,
        (int SerializerId, string Manifest, byte[] Bytes) payload,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            """
            INSERT INTO snapshot (persistence_id, sequence_number, created, snapshot, manifest,
                                  serializer_id)
            VALUES ($persistenceId, $sequenceNr, $created, $snapshot, $manifest, $serializerId)
            """,
            [
                ("$persistenceId", persistenceId),
                ("$sequenceNr", sequenceNr),
                ("$created", Payloads.At.UtcTicks),
                ("$snapshot", payload.Bytes),
                ("$manifest", payload.Manifest),
                ("$serializerId", payload.SerializerId),
            ],
            cancellationToken);

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(Path);
        return ValueTask.CompletedTask;
    }

    private async Task ExecuteAsync(
        string sql, (string Name, object Value)[] parameters, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
