using Npgsql;

namespace Ohmline.Billing.Feed;

/// <summary>
/// Where Billing is in each feed it reads: <c>billing.inbox_position</c>, one row per feed,
/// written in the same database transaction as whatever the entry at that position changed.
/// </summary>
public static class Inbox
{
    public const string ChargingFeed = "charging";

    public static async Task<long> PositionAsync(
        NpgsqlDataSource database, string feed, CancellationToken cancellationToken)
    {
        await using var select = database.CreateCommand(
            "SELECT position FROM billing.inbox_position WHERE feed = @feed");
        select.Parameters.AddWithValue("feed", feed);
        return await select.ExecuteScalarAsync(cancellationToken) is long position ? position : 0;
    }

    /// <summary>Moves the position forward, never back: a late write changes nothing.</summary>
    public static async Task MoveToAsync(
        NpgsqlTransaction transaction,
        string feed,
        long position,
        CancellationToken cancellationToken)
    {
        await using var upsert = new NpgsqlCommand(
            """
            INSERT INTO billing.inbox_position AS current (feed, position)
            VALUES (@feed, @position)
            ON CONFLICT (feed) DO UPDATE SET position = excluded.position
            WHERE excluded.position > current.position
            """,
            transaction.Connection,
            transaction);
        upsert.Parameters.AddWithValue("feed", feed);
        upsert.Parameters.AddWithValue("position", position);
        await upsert.ExecuteNonQueryAsync(cancellationToken);
    }
}
