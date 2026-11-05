using Npgsql;

namespace Ohmline.Billing.Holds;

/// <summary>The SQL of <c>billing.holds</c>.</summary>
public sealed class HoldStore(NpgsqlDataSource database, TimeProvider clock)
{
    public async Task<Hold?> FindAsync(string holdId, CancellationToken cancellationToken)
    {
        await using var select = database.CreateCommand("""
            SELECT hold_id, driver_ref, amount, currency, status, captured
            FROM billing.holds
            WHERE hold_id = @hold_id
            """);
        select.Parameters.AddWithValue("hold_id", holdId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new Hold(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5))
            : null;
    }

    /// <summary>
    /// Stores a new hold, unless one with its id exists: then the stored one wins, and is
    /// returned, so placing the same hold twice holds once.
    /// </summary>
    public async Task<(Hold Hold, bool Added)> AddAsync(
        Hold hold, CancellationToken cancellationToken)
    {
        await using var insert = database.CreateCommand("""
            INSERT INTO billing.holds
                (hold_id, driver_ref, amount, currency, status, captured, created_at, updated_at)
            VALUES (@hold_id, @driver_ref, @amount, @currency, @status, NULL, @now, @now)
            ON CONFLICT (hold_id) DO NOTHING
            """);
        insert.Parameters.AddWithValue("hold_id", hold.HoldId);
        insert.Parameters.AddWithValue("driver_ref", hold.DriverRef);
        insert.Parameters.AddWithValue("amount", hold.Amount);
        insert.Parameters.AddWithValue("currency", hold.Currency);
        insert.Parameters.AddWithValue("status", hold.Status);
        insert.Parameters.AddWithValue("now", clock.GetUtcNow());
        var added = await insert.ExecuteNonQueryAsync(cancellationToken) == 1;
        return added
            ? (hold, true)
            : ((await FindAsync(hold.HoldId, cancellationToken))!, false);
    }

    /// <summary>
    /// Moves the hold on from the status it was read in, and only from that one: a request that
    /// raced another and lost changes nothing, and is told so.
    /// </summary>
    public async Task<bool> MoveAsync(
        Hold from, Hold to, CancellationToken cancellationToken)
    {
        await using var update = database.CreateCommand("""
            UPDATE billing.holds
            SET status = @status, captured = @captured, updated_at = @now
            WHERE hold_id = @hold_id AND status = @expected
            """);
        update.Parameters.AddWithValue("hold_id", from.HoldId);
        update.Parameters.AddWithValue("expected", from.Status);
        update.Parameters.AddWithValue("status", to.Status);
        update.Parameters.AddWithValue("captured", (object?)to.Captured ?? DBNull.Value);
        update.Parameters.AddWithValue("now", clock.GetUtcNow());
        return await update.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
