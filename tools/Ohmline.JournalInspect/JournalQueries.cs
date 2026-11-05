namespace Ohmline.JournalInspect;

/// <summary>
/// The journal tour's SELECT statements (section 10.8), for PostgreSQL as well as SQLite:
/// Akka.Persistence.Sql gives its tables and columns the same names on both. Only the parameters
/// changed, from SQLite's <c>$name</c> to <c>@name</c>, which both drivers accept.
/// </summary>
public static class JournalQueries
{
    /// <summary>One aggregate's events in order. The message column holds the payload.</summary>
    public const string Journal = """
        SELECT ordering, persistence_id, sequence_number, created, deleted,
               manifest, identifier, writer_uuid, message
        FROM journal
        WHERE persistence_id = @persistenceId
        ORDER BY sequence_number
        """;

    /// <summary>Every aggregate's events in the journal's global order, after a position.</summary>
    public const string Recent = """
        SELECT ordering, persistence_id, sequence_number, created, deleted,
               manifest, identifier, writer_uuid, message
        FROM journal
        WHERE ordering > @after
        ORDER BY ordering
        LIMIT @limit
        """;

    /// <summary>One aggregate's snapshots. The snapshot column holds the payload bytes.</summary>
    public const string Snapshots = """
        SELECT persistence_id, sequence_number, created, manifest, serializer_id, snapshot
        FROM snapshot
        WHERE persistence_id = @persistenceId
        ORDER BY sequence_number
        """;
}
