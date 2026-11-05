using Npgsql;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.Charging.ReadModels.SiteLiveBoard;

/// <summary>
/// The SQL of <c>read_models.site_live_board</c>: one row per connector. Two journals feed
/// each row, a charge point's and its site's budget, so each half of the row has a guard of its
/// own: <c>status_sequence_nr</c> from the charge point, <c>allocation_sequence_nr</c> from the
/// site. Either half may arrive first.
/// </summary>
public static class SiteLiveBoardTable
{
    public static async Task ApplyAsync(
        NpgsqlTransaction transaction,
        ConnectorStatusChange change,
        long sequenceNr,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            change.SiteId is null
                ? """
                  UPDATE read_models.site_live_board
                  SET status = @status, session_id = @session_id, status_sequence_nr = @sequence_nr
                  WHERE connector_id = @connector_id AND status_sequence_nr < @sequence_nr
                  """
                : """
                  INSERT INTO read_models.site_live_board AS board
                      (connector_id, site_id, status, session_id, status_sequence_nr)
                  VALUES (@connector_id, @site_id, @status, @session_id, @sequence_nr)
                  ON CONFLICT (connector_id) DO UPDATE SET
                      site_id = excluded.site_id,
                      status = excluded.status,
                      session_id = excluded.session_id,
                      status_sequence_nr = excluded.status_sequence_nr
                  WHERE excluded.status_sequence_nr > board.status_sequence_nr
                  """,
            transaction.Connection,
            transaction);
        command.Parameters.AddWithValue("connector_id", change.ConnectorId);
        command.Parameters.AddWithValue("site_id", (object?)change.SiteId ?? DBNull.Value);
        command.Parameters.AddWithValue("status", change.Status);
        command.Parameters.AddWithValue(
            "session_id", (object?)change.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("sequence_nr", sequenceNr);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task ApplyAsync(
        NpgsqlTransaction transaction,
        ISiteAllocationEvent allocations,
        long sequenceNr,
        CancellationToken cancellationToken)
    {
        foreach (var allocation in allocations.Allocations)
        {
            await using var upsert = new NpgsqlCommand(
                """
                INSERT INTO read_models.site_live_board AS board
                    (connector_id, site_id, allocated_kw, allocation_sequence_nr)
                VALUES (@connector_id, @site_id, @allocated_kw, @sequence_nr)
                ON CONFLICT (connector_id) DO UPDATE SET
                    allocated_kw = excluded.allocated_kw,
                    allocation_sequence_nr = excluded.allocation_sequence_nr
                WHERE excluded.allocation_sequence_nr > board.allocation_sequence_nr
                """,
                transaction.Connection,
                transaction);
            upsert.Parameters.AddWithValue("connector_id", allocation.ConnectorId.ToString());
            upsert.Parameters.AddWithValue("site_id", allocations.SiteId.Value);
            upsert.Parameters.AddWithValue(
                "allocated_kw", (object?)allocation.Allocated?.Kilowatts ?? DBNull.Value);
            upsert.Parameters.AddWithValue("sequence_nr", sequenceNr);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        if (allocations.CoversWholeSite)
        {
            // Every other connector at the site that still has a request waits now. The rows
            // just written carry this sequence number already, so the guard leaves them alone.
            await using var queued = new NpgsqlCommand(
                """
                UPDATE read_models.site_live_board
                SET allocated_kw = 0, allocation_sequence_nr = @sequence_nr
                WHERE site_id = @site_id
                  AND allocated_kw IS NOT NULL
                  AND allocation_sequence_nr < @sequence_nr
                """,
                transaction.Connection,
                transaction);
            queued.Parameters.AddWithValue("site_id", allocations.SiteId.Value);
            queued.Parameters.AddWithValue("sequence_nr", sequenceNr);
            await queued.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
