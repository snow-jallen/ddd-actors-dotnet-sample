using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// The phone's only durable record of the driver's intent (section 19.6): every command,
/// written to SQLite before any network call, with what became of it. A command survives a
/// dead network, a killed app and a reboot. The head passes the connection string for a file
/// in the app's data folder, <c>ohmline-driver.db3</c>; a test passes a temporary file.
/// </summary>
/// <remarks>
/// Not a journal: it holds commands, which are intent, never events, which are facts, and only
/// until the server has answered. One writer at a time is enough on a phone; WAL lets the
/// screens read while the dispatcher writes.
/// </remarks>
public sealed class SqliteOutbox(string connectionString)
{
    private const string Columns = """
        command_id, kind, aggregate_key, payload, created_utc, attempts, status, version, code,
        reason
        """;

    private readonly Lazy<Task> _created = new(() => CreateAsync(connectionString));

    /// <summary>
    /// Writes the command as queued, unless a command with its id is there already: a double
    /// tap, or the same command enqueued again, is one row. True when it was written.
    /// </summary>
    public async Task<bool> EnqueueAsync(
        OutboxCommand command, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT OR IGNORE INTO outbox
                (command_id, kind, aggregate_key, payload, created_utc, attempts, status)
            VALUES ($id, $kind, $key, $payload, $created, 0, 'queued')
            """;
        insert.Parameters.AddWithValue("$id", command.CommandId.ToString());
        insert.Parameters.AddWithValue("$kind", command.Kind);
        insert.Parameters.AddWithValue("$key", command.AggregateKey);
        insert.Parameters.AddWithValue("$payload", command.Payload);
        insert.Parameters.AddWithValue("$created", Stamp(now));
        return await insert.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    /// <summary>The queued commands, in the order they were given.</summary>
    public Task<IReadOnlyList<OutboxRow>> ReadQueuedAsync(CancellationToken cancellationToken) =>
        ReadAsync($"WHERE status = '{OutboxStatus.Queued}' ORDER BY rowid", [], cancellationToken);

    /// <summary>Every command for one aggregate, oldest first: what a screen overlays.</summary>
    public Task<IReadOnlyList<OutboxRow>> ReadForAsync(
        string aggregateKey, CancellationToken cancellationToken) =>
        ReadAsync("WHERE aggregate_key = $key ORDER BY rowid", [("$key", aggregateKey)],
            cancellationToken);

    /// <summary>
    /// Every command made since <paramref name="since"/>, oldest first: what a screen overlays
    /// on the cached cards until the server's read model shows it.
    /// </summary>
    public Task<IReadOnlyList<OutboxRow>> ReadSinceAsync(
        DateTimeOffset since, CancellationToken cancellationToken) =>
        ReadAsync("WHERE created_utc >= $since ORDER BY rowid", [("$since", Stamp(since))],
            cancellationToken);

    public async Task<OutboxRow?> FindAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var rows = await ReadAsync(
            "WHERE command_id = $id", [("$id", commandId.ToString())], cancellationToken);
        return rows is [var row] ? row : null;
    }

    /// <summary>Counts an attempt before it is made: one the app dies in still counts.</summary>
    public Task MarkAttemptAsync(Guid commandId, CancellationToken cancellationToken) =>
        UpdateAsync("attempts = attempts + 1", commandId, [], cancellationToken);

    public Task MarkAcceptedAsync(
        Guid commandId, long version, CancellationToken cancellationToken) =>
        UpdateAsync(
            $"status = '{OutboxStatus.Accepted}', version = $version",
            commandId,
            [("$version", version)],
            cancellationToken);

    public Task MarkRejectedAsync(
        Guid commandId, string code, string reason, CancellationToken cancellationToken) =>
        UpdateAsync(
            $"status = '{OutboxStatus.Rejected}', code = $code, reason = $reason",
            commandId,
            [("$code", code), ("$reason", reason)],
            cancellationToken);

    /// <summary>
    /// Marks every queued command made before <paramref name="cutoff"/> as failed to sync, and
    /// returns their ids: the app has given up on them, and tells the driver.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ExpireAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var expire = connection.CreateCommand();
        expire.CommandText = $"""
            UPDATE outbox SET status = '{OutboxStatus.FailedToSync}'
            WHERE status = '{OutboxStatus.Queued}' AND created_utc < $cutoff
            RETURNING command_id
            """;
        expire.Parameters.AddWithValue("$cutoff", Stamp(cutoff));
        var expired = new List<Guid>();
        await using var reader = await expire.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            expired.Add(Guid.Parse(reader.GetString(0)));
        }

        return expired;
    }

    private static async Task CreateAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS outbox (
                command_id    TEXT PRIMARY KEY,
                kind          TEXT NOT NULL,
                aggregate_key TEXT NOT NULL,
                payload       TEXT NOT NULL,
                created_utc   TEXT NOT NULL,
                attempts      INTEGER NOT NULL,
                status        TEXT NOT NULL,
                version       INTEGER NULL,
                code          TEXT NULL,
                reason        TEXT NULL,
                trace_parent  TEXT NULL
            );
            """;
        await create.ExecuteNonQueryAsync();
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await _created.Value.WaitAsync(cancellationToken);
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task<IReadOnlyList<OutboxRow>> ReadAsync(
        string where,
        (string Name, object Value)[] parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var select = connection.CreateCommand();
        select.CommandText = $"SELECT {Columns} FROM outbox {where}";
        foreach (var (name, value) in parameters)
        {
            select.Parameters.AddWithValue(name, value);
        }

        var rows = new List<OutboxRow>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new OutboxRow(
                CommandId: Guid.Parse(reader.GetString(0)),
                Kind: reader.GetString(1),
                AggregateKey: reader.GetString(2),
                Payload: reader.GetString(3),
                CreatedUtc: DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                Attempts: reader.GetInt32(5),
                Status: reader.GetString(6),
                Version: reader.IsDBNull(7) ? null : reader.GetInt64(7),
                Code: reader.IsDBNull(8) ? null : reader.GetString(8),
                Reason: reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return rows;
    }

    private async Task UpdateAsync(
        string set,
        Guid commandId,
        (string Name, object Value)[] parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var update = connection.CreateCommand();
        update.CommandText = $"UPDATE outbox SET {set} WHERE command_id = $id";
        update.Parameters.AddWithValue("$id", commandId.ToString());
        foreach (var (name, value) in parameters)
        {
            update.Parameters.AddWithValue(name, value);
        }

        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>UTC, to the tick, in a form whose text order is its time order.</summary>
    private static string Stamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
