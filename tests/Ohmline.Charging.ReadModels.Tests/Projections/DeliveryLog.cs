using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.ReadModels.Projections;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// A projection that only writes down what it was given: one row per delivery, in the order of
/// delivery, so a test can see an event that came twice, or never. It can fail once, on
/// purpose, after its write, at one offset.
/// </summary>
internal sealed class DeliveryLog(long? failOnceAt = null) : IProjectionHandler
{
    private bool _failed;

    public string ProjectionId => "delivery-log";

    public IReadOnlyList<string> Tags => [ChargingTags.Charging];

    public static ProjectionStream Stream { get; } = new("delivery-log", ChargingTags.Charging);

    public async Task HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var offset = ((Sequence)envelope.Offset).Value;
        await using var insert = new NpgsqlCommand(
            "INSERT INTO deliveries (journal_offset, persistence_id, sequence_nr) " +
            "VALUES (@journal_offset, @persistence_id, @sequence_nr)",
            transaction.Connection,
            transaction);
        insert.Parameters.AddWithValue("journal_offset", offset);
        insert.Parameters.AddWithValue("persistence_id", envelope.PersistenceId);
        insert.Parameters.AddWithValue("sequence_nr", envelope.SequenceNr);
        await insert.ExecuteNonQueryAsync(cancellationToken);

        if (offset == failOnceAt && !_failed)
        {
            _failed = true;
            throw new InvalidOperationException($"Failed on purpose, once, at offset {offset}.");
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
