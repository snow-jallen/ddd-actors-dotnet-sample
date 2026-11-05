using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Ohmline.JournalInspect;

/// <summary>
/// What to read, and from where: <c>--postgres &lt;connection string&gt;</c> or
/// <c>--sqlite &lt;file&gt;</c>; then <c>--persistence-id &lt;id&gt;</c> for one aggregate (with
/// <c>--snapshots</c> for its snapshots instead of its events), or <c>--after &lt;ordering&gt;</c>
/// and <c>--limit &lt;n&gt;</c> for every aggregate's events in the journal's order.
/// </summary>
public sealed record InspectOptions(
    string Provider,
    string ConnectionString,
    string? PersistenceId,
    bool Snapshots,
    long After,
    int Limit)
{
    public const string Usage = """
        Usage: Ohmline.JournalInspect (--postgres <connection string> | --sqlite <file>)
                                      [--persistence-id <id> [--snapshots]]
                                      [--after <ordering>] [--limit <n>]
        """;

    /// <summary>The options, or null when the arguments do not name exactly one database.</summary>
    public static InspectOptions? Parse(IReadOnlyList<string> args)
    {
        var flags = args.Where(arg => arg == "--snapshots").ToHashSet();
        var named = args.Except(flags).Chunk(2)
            .Where(pair => pair is [var name, _] && name.StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(pair => pair[0][2..], pair => pair[1]);
        var (provider, connectionString) =
            (named.GetValueOrDefault("postgres"), named.GetValueOrDefault("sqlite")) switch
            {
                ({ } postgres, null) => ("postgres", postgres),
                (null, { } file) => ("sqlite", $"Data Source={file};Mode=ReadOnly"),
                _ => (null, null),
            };
        return provider is null || connectionString is null
            ? null
            : new InspectOptions(
                provider,
                connectionString,
                named.GetValueOrDefault("persistence-id"),
                flags.Count > 0,
                long.Parse(named.GetValueOrDefault("after") ?? "0", CultureInfo.InvariantCulture),
                int.Parse(named.GetValueOrDefault("limit") ?? "100", CultureInfo.InvariantCulture));
    }

    public DbConnection Connect() => Provider switch
    {
        "postgres" => new NpgsqlConnection(ConnectionString),
        _ => new SqliteConnection(ConnectionString),
    };

    /// <summary>The rows these options ask for.</summary>
    public Task<IReadOnlyList<StoredRow>> ReadAsync(
        JournalReader reader, CancellationToken cancellationToken) =>
        (PersistenceId, Snapshots) switch
        {
            ({ } id, true) => reader.SnapshotsOfAsync(id, cancellationToken),
            ({ } id, false) => reader.EventsOfAsync(id, cancellationToken),
            (null, _) => reader.EventsAfterAsync(After, Limit, cancellationToken),
        };
}
