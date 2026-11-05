using System.Collections.Immutable;
using Akka.Persistence.Query;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.SessionHistory;
using Ohmline.Charging.ReadModels.Tests.Projections;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using static Ohmline.Charging.ReadModels.Tests.Known;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.ReadModels.Tests.SessionHistory;

/// <summary>
/// <see cref="SessionHistoryProjection"/> against PostgreSQL: idempotent when an envelope comes
/// again, and kept up to date by the runner from a real journal.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class SessionHistoryProjectionTests(PostgresFixture postgres)
{
    private static readonly string PersistenceId = $"charging-session-{Session}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_the_same_envelope_twice_then_one_row_and_its_version_does_not_move()
    {
        await using var database = await ReadModelsAsync();
        var projection = new SessionHistoryProjection();
        var started = Envelope(1, new SessionStarted(
            Session,
            Depot,
            new ConnectorId(Charger, 1),
            DriversCard,
            Tariff,
            new Money(40m, Currency.Usd),
            MeterStartWh: 1_200_000) { OccurredAt = Morning });
        var recorded = Envelope(
            2,
            new MeterValueRecorded(Session, Energy.FromWattHours(1_207_350), Morning.AddHours(1)));

        foreach (var envelope in new[] { started, recorded, recorded, started })
        {
            await DeliverAsync(database, projection, envelope);
        }

        (await CountAsync(database)).ShouldBe(1);
        var row = (await FindAsync(database)).ShouldNotBeNull();
        row.LastSequenceNr.ShouldBe(2);
        row.EnergyKwh.ShouldBe(7.35m);
        row.Status.ShouldBe("charging");
    }

    [Fact]
    public async Task Given_the_same_envelope_twice_then_each_version_is_announced_once()
    {
        await using var database = await ReadModelsAsync();
        var projection = new SessionHistoryProjection();
        var started = Envelope(1, new SessionStarted(
            Session,
            Depot,
            new ConnectorId(Charger, 1),
            DriversCard,
            Tariff,
            new Money(40m, Currency.Usd),
            MeterStartWh: 1_200_000) { OccurredAt = Morning });
        var recorded = Envelope(
            2,
            new MeterValueRecorded(Session, Energy.FromWattHours(1_207_350), Morning.AddHours(1)));

        var announced = new List<LiveUpdate>();
        foreach (var envelope in new[] { started, recorded, recorded, started })
        {
            announced.AddRange(await DeliverAsync(database, projection, envelope));
        }

        announced.ShouldBe(
        [
            new SessionSummaryChanged(Session, 1, "charging", 0m),
            new SessionSummaryChanged(Session, 2, "charging", 7.35m),
        ]);
    }

    [Fact]
    public async Task Given_a_session_in_the_journal_then_the_runner_keeps_its_row_up_to_date()
    {
        using var journal = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await JournalNode.StartAsync(journal, clock, Ct);
        await using var database = await ReadModelsAsync();
        (await node.AskAsync<ChargingSessionRegion, CommandReply>(Begin(), Ct))
            .ShouldBe(new CommandAccepted(1));
        clock.Advance(TimeSpan.FromMinutes(30));
        (await node.AskAsync<ChargingSessionRegion, CommandReply>(
                new RecordMeterValue(Command, Session, 1_211_000, clock.GetUtcNow()), Ct))
            .ShouldBe(new CommandAccepted(2));
        (await node.AskAsync<ChargingSessionRegion, CommandReply>(
                new StopSession(Command, Session), Ct))
            .ShouldBe(new CommandAccepted(3));

        await ProjectionRun.UntilOffsetAsync(
            node,
            database,
            new NpgsqlOffsetStore(database),
            new SessionHistoryProjection(),
            ChargingTags.ChargingSession,
            offset: 3,
            Ct);

        (await FindAsync(database)).ShouldBe(new SessionHistoryRow(
            Session.Value,
            "04A2B3C4D5E6F7",
            "OHM-SLC-0042/1",
            "ended",
            EnergyKwh: 11m,
            MeterStartWh: 1_200_000,
            StartedAt: Morning,
            EndedAt: Morning.AddMinutes(30),
            LastSequenceNr: 3));
    }

    private static EventEnvelope Envelope(long sequenceNr, SessionEvent @event) =>
        new(Offset.Sequence(sequenceNr), PersistenceId, sequenceNr, @event, 0L,
            [ChargingTags.ChargingSession]);

    /// <summary>What the runner does with one envelope, without the offset.</summary>
    private static async Task<ImmutableArray<LiveUpdate>> DeliverAsync(
        NpgsqlDataSource database, IProjectionHandler projection, EventEnvelope envelope)
    {
        await using var connection = await database.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var changes = await projection.HandleAsync(envelope, transaction, Ct);
        await transaction.CommitAsync(Ct);
        return changes;
    }

    private static async Task<SessionHistoryRow?> FindAsync(NpgsqlDataSource database)
    {
        await using var connection = await database.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        return await SessionHistoryTable.FindAsync(transaction, Session.Value, Ct);
    }

    private static async Task<long> CountAsync(NpgsqlDataSource database)
    {
        await using var count =
            database.CreateCommand("SELECT count(*) FROM read_models.session_history");
        return (long)(await count.ExecuteScalarAsync(Ct))!;
    }

    private async Task<NpgsqlDataSource> ReadModelsAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await ReadModelSchema.EnsureAsync(database, Ct);
        return database;
    }
}
