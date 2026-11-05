using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.ReadModels.SiteLiveBoard;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Domain.Budgets;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.SiteLiveBoard;

/// <summary>
/// <see cref="SiteLiveBoardProjection"/> against PostgreSQL, fed by hand from both of its
/// tags: two charge points at the depot, and the depot's power budget.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class SiteLiveBoardProjectionTests(PostgresFixture postgres)
{
    private static readonly ConnectorId First = new(Charger, 1);

    private static readonly ConnectorId Second = new(NextCharger, 1);

    private static readonly SessionId OtherSession = new(Guid.CreateVersion7(Morning.AddHours(1)));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_grant_before_the_registration_then_the_connector_shows_both()
    {
        await using var database = await ReadModelsAsync();

        await DeliverAsync(database, Budget(2, new AllocationGranted(
            Depot, First, Session, new Power(50m), new Power(32m))));
        await DeliverAsync(database, ChargePoint(Charger, 1, Registered(Charger)));

        (await BoardAsync(database)).ShouldBe(
        [
            new Row("OHM-SLC-0042/1", "available", null, 32m),
            new Row("OHM-SLC-0042/2", "available", null, null),
        ]);
    }

    [Fact]
    public async Task Given_a_rebalance_then_a_connector_it_leaves_out_waits_with_nothing()
    {
        await using var database = await ReadModelsAsync();
        await DeliverAsync(database, ChargePoint(Charger, 1, Registered(Charger)));
        await DeliverAsync(database, ChargePoint(NextCharger, 1, Registered(NextCharger)));
        await DeliverAsync(database, ChargePoint(
            Charger, 2, new ConnectorOccupied(Charger, 1, Session, DriversCard)));

        await DeliverAsync(database, Budget(2, new AllocationGranted(
            Depot, First, Session, new Power(50m), new Power(50m))));
        await DeliverAsync(database, Budget(3, new AllocationGranted(
            Depot, Second, OtherSession, new Power(50m), new Power(50m))));
        await DeliverAsync(database, Budget(4, new AllocationsRebalanced(
            Depot, [new PowerAllocation(First, new Power(30m))])));

        (await BoardAsync(database)).ShouldBe(
        [
            new Row("OHM-SLC-0042/1", "occupied", Session.Value, 30m),
            new Row("OHM-SLC-0042/2", "available", null, null),
            new Row("OHM-SLC-0043/1", "available", null, 0m),
            new Row("OHM-SLC-0043/2", "available", null, null),
        ]);
    }

    [Fact]
    public async Task Given_every_envelope_again_in_reverse_then_the_board_does_not_change()
    {
        await using var database = await ReadModelsAsync();
        EventEnvelope[] envelopes =
        [
            ChargePoint(Charger, 1, Registered(Charger)),
            ChargePoint(Charger, 2, new ConnectorOccupied(Charger, 1, Session, DriversCard)),
            Budget(2, new AllocationGranted(Depot, First, Session, new(50m), new(50m))),
            Budget(3, new AllocationReleased(Depot, First, Session)),
            ChargePoint(Charger, 3, new ConnectorReleased(Charger, 1, Session)),
        ];
        foreach (var envelope in envelopes)
        {
            await DeliverAsync(database, envelope);
        }

        var board = await BoardAsync(database);
        foreach (var envelope in envelopes.Reverse())
        {
            await DeliverAsync(database, envelope);
        }

        (await BoardAsync(database)).ShouldBe(board);
        board[0].ShouldBe(new Row("OHM-SLC-0042/1", "available", null, null));
    }

    private static ChargePointRegistered Registered(ChargePointId chargePoint) =>
        new(chargePoint, Depot, [new Power(22m), new Power(22m)]);

    private static EventEnvelope ChargePoint(
        ChargePointId chargePoint, long sequenceNr, ChargePointEvent @event) =>
        Envelope($"charge-point-{chargePoint}", sequenceNr, @event, ChargingTags.Charging);

    private static EventEnvelope Budget(long sequenceNr, SitePowerBudgetEvent @event) =>
        Envelope($"site-power-budget-{Depot}", sequenceNr, @event, SiteEnergyTags.SiteEnergy);

    /// <summary>As the read journal delivers it; the projection ignores the offset.</summary>
    private static EventEnvelope Envelope(
        string persistenceId, long sequenceNr, object @event, string tag) =>
        new(Offset.Sequence(0), persistenceId, sequenceNr, @event, 0L, [tag]);

    private static async Task DeliverAsync(NpgsqlDataSource database, EventEnvelope envelope)
    {
        await using var connection = await database.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await new SiteLiveBoardProjection().HandleAsync(envelope, transaction, Ct);
        await transaction.CommitAsync(Ct);
    }

    private static async Task<IReadOnlyList<Row>> BoardAsync(NpgsqlDataSource database)
    {
        await using var select = database.CreateCommand(
            "SELECT connector_id, status, session_id, allocated_kw " +
            "FROM read_models.site_live_board WHERE site_id = 'slc-depot' ORDER BY connector_id");
        await using var reader = await select.ExecuteReaderAsync(Ct);
        var rows = new List<Row>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add(new Row(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetDecimal(3)));
        }

        return rows;
    }

    private async Task<NpgsqlDataSource> ReadModelsAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await ReadModelSchema.EnsureAsync(database, Ct);
        return database;
    }

    private sealed record Row(
        string ConnectorId, string Status, Guid? SessionId, decimal? AllocatedKw);
}
