using System.Text.Json;
using Microsoft.Data.Sqlite;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.ReadModels;

namespace Ohmline.Mobile.Core.ReadModels;

/// <summary>
/// The driver's session cards as the server last sent them, kept on the phone (section 19.7),
/// so the app shows the last known state at once, also offline. Disposable: nothing here is a
/// source of truth, and an app that deletes it fills it again from the server's feed.
/// </summary>
/// <remarks>
/// Pushes may arrive twice, late or out of order, and the feed repeats what a push already
/// brought. The upsert keeps a card only if its version is higher than the one cached, and the
/// feed position only moves forward, so applying the same change twice, or an older one after
/// a newer one, changes nothing.
/// </remarks>
public sealed class SqliteReadModelCache(string connectionString)
{
    private static readonly ChargingClientJsonContext Json = ChargingClientJsonContext.Default;

    private readonly Lazy<Task> _created = new(() => CreateAsync(connectionString));

    /// <summary>
    /// Keeps the card if it is newer than the one cached, and moves the token's feed position
    /// to <paramref name="position"/> if that is further on. True when the card was kept.
    /// </summary>
    public async Task<bool> ApplyAsync(
        DriverSessionCard card, long position, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var upsert = connection.CreateCommand();
        upsert.CommandText = """
            INSERT INTO session_card (token_uid, session_id, version, json)
            VALUES ($token, $session, $version, $json)
            ON CONFLICT (token_uid, session_id) DO UPDATE
                SET version = excluded.version, json = excluded.json
                WHERE session_card.version < excluded.version
            """;
        upsert.Parameters.AddWithValue("$token", card.TokenUid);
        upsert.Parameters.AddWithValue("$session", card.SessionId.ToString());
        upsert.Parameters.AddWithValue("$version", card.Version);
        upsert.Parameters.AddWithValue(
            "$json", JsonSerializer.Serialize(card, Json.DriverSessionCard));
        var kept = await upsert.ExecuteNonQueryAsync(cancellationToken) == 1;

        await using var advance = connection.CreateCommand();
        advance.CommandText = """
            INSERT INTO feed_position (token_uid, position) VALUES ($token, $position)
            ON CONFLICT (token_uid) DO UPDATE SET position = excluded.position
                WHERE feed_position.position < excluded.position
            """;
        advance.Parameters.AddWithValue("$token", card.TokenUid);
        advance.Parameters.AddWithValue("$position", position);
        await advance.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return kept;
    }

    /// <summary>The token's cards, the newest session first.</summary>
    public async Task<IReadOnlyList<DriverSessionCard>> ReadCardsAsync(
        string tokenUid, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT json FROM session_card WHERE token_uid = $token";
        select.Parameters.AddWithValue("$token", tokenUid);
        var cards = new List<DriverSessionCard>();
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cards.Add(JsonSerializer.Deserialize(reader.GetString(0), Json.DriverSessionCard)!);
        }

        return [.. cards.OrderByDescending(card => card.StartedAt)];
    }

    /// <summary>How far the token's feed has been read: 0 before the first entry.</summary>
    public async Task<long> ReadPositionAsync(string tokenUid, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT position FROM feed_position WHERE token_uid = $token";
        select.Parameters.AddWithValue("$token", tokenUid);
        return await select.ExecuteScalarAsync(cancellationToken) is long position ? position : 0;
    }

    private static async Task CreateAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS session_card (
                token_uid  TEXT NOT NULL,
                session_id TEXT NOT NULL,
                version    INTEGER NOT NULL,
                json       TEXT NOT NULL,
                PRIMARY KEY (token_uid, session_id)
            );
            CREATE TABLE IF NOT EXISTS feed_position (
                token_uid TEXT PRIMARY KEY,
                position  INTEGER NOT NULL
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
}
