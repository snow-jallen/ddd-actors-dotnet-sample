using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.ReadModels.Projections;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// A projection that only writes down what it was given: one row per delivery, in the order of
/// delivery, so a test can see an event that came twice, or never. It can fail on purpose,
/// after its write, at one offset: once, every time, or with a lost connection a few times.
/// </summary>
internal sealed class DeliveryLog(
    long? failOnceAt = null,
    long? failAlwaysAt = null,
    (long Offset, int Times)? connectionLostAt = null,
    ProjectionFailurePolicy? policy = null) : IProjectionHandler
{
    private readonly Dictionary<long, int> _attempts = [];

    public string ProjectionId => "delivery-log";

    public IReadOnlyList<string> Tags => [ChargingTags.Charging];

    public ProjectionFailurePolicy FailurePolicy { get; } = policy ?? new StopAfterRetries(5);

    public static ProjectionStream Stream { get; } = new("delivery-log", ChargingTags.Charging);

    /// <summary>How many times the projection was handed the event at the offset.</summary>
    public int AttemptsAt(long offset) => _attempts.GetValueOrDefault(offset);

    public async Task HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var offset = ((Sequence)envelope.Offset).Value;
        var attempt = _attempts[offset] = AttemptsAt(offset) + 1;
        await using var insert = new NpgsqlCommand(
            "INSERT INTO deliveries (journal_offset, persistence_id, sequence_nr) " +
            "VALUES (@journal_offset, @persistence_id, @sequence_nr)",
            transaction.Connection,
            transaction);
        insert.Parameters.AddWithValue("journal_offset", offset);
        insert.Parameters.AddWithValue("persistence_id", envelope.PersistenceId);
        insert.Parameters.AddWithValue("sequence_nr", envelope.SequenceNr);
        await insert.ExecuteNonQueryAsync(cancellationToken);

        if (offset == failOnceAt && attempt == 1 || offset == failAlwaysAt)
        {
            throw new InvalidOperationException($"Failed on purpose at offset {offset}.");
        }

        if (connectionLostAt is var (lostAt, times) && offset == lostAt && attempt <= times)
        {
            throw new NpgsqlException("The connection was lost.", new IOException("Reset."));
        }
    }

    public static async Task CreateTableAsync(
        NpgsqlDataSource database, CancellationToken cancellationToken)
    {
        await using var create = database.CreateCommand("""
            CREATE TABLE deliveries (
                delivery       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                journal_offset bigint NOT NULL,
                persistence_id text   NOT NULL,
                sequence_nr    bigint NOT NULL
            )
            """);
        await create.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>The offset of every delivery, in the order they were committed.</summary>
    public static async Task<IReadOnlyList<long>> OffsetsAsync(
        NpgsqlDataSource database, CancellationToken cancellationToken)
    {
        await using var select = database.CreateCommand(
            "SELECT journal_offset FROM deliveries ORDER BY delivery");
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        var offsets = new List<long>();
        while (await reader.ReadAsync(cancellationToken))
        {
            offsets.Add(reader.GetInt64(0));
        }

        return offsets;
    }
}
