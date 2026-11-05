using System.Text.Json;
using Akka.Streams;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Integration;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.ReadModels.Outbox;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.Tests.Projections;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Outbox;

/// <summary>
/// The relay in the projection runner, over a session that Charging's own actor wrote to the
/// in-memory journal: one integration event in the outbox, written with the offset, and still
/// one after the journal is read again from the start.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class IntegrationEventRelayTests(PostgresFixture postgres)
{
    private const string Tag = ChargingTags.ChargingSession;

    private static readonly ProjectionStream Stream = new(IntegrationEventRelay.Id, Tag);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_session_that_ended_then_the_outbox_holds_it_as_completed()
    {
        await using var journal = await ChargedSessionAsync();
        await using var database = await ReadModelsAsync();

        await RelayAsync(journal, database);

        var (position, type, payload) = (await OutboxAsync(database)).ShouldHaveSingleItem();
        position.ShouldBe(1);
        type.ShouldBe("ohmline.charging.session-completed.v1");
        var completed = JsonSerializer
            .Deserialize<ChargingIntegrationEvent>(payload, PublishedLanguage.Json)
            .ShouldBeOfType<SessionCompleted>();
        completed.SessionId.ShouldBe(Session.Value);
        completed.EnergyKwh.ShouldBe(30m);
        completed.Price.Total.ShouldBe(12.60m);
        (await CountAsync(database, "integration_relay_sessions")).ShouldBe(0);
    }

    [Fact]
    public async Task Given_the_journal_read_again_from_the_start_then_nothing_is_published_twice()
    {
        await using var journal = await ChargedSessionAsync();
        await using var database = await ReadModelsAsync();
        await RelayAsync(journal, database);

        await ResetOffsetAsync(database);
        await RelayAsync(journal, database);

        (await OutboxAsync(database)).ShouldHaveSingleItem();
        (await CountAsync(database, "integration_relay_sessions")).ShouldBe(0);
    }

    private static async Task<MemoryJournal> ChargedSessionAsync()
    {
        var journal = await MemoryJournal.StartAsync(Ct);
        var clock = new FakeTimeProvider(Morning);
        var session = journal.System.ActorOf(ChargingSessionActor.Props(Session, clock));
        await journal.AskAsync<CommandReply>(session, Begin(), Ct);
        clock.Advance(TimeSpan.FromMinutes(45));
        await journal.AskAsync<CommandReply>(
            session, new RecordMeterValue(Command, Session, 1_230_000, clock.GetUtcNow()), Ct);
        await journal.AskAsync<CommandReply>(session, new StopSession(Command, Session), Ct);
        return journal;
    }

    private async Task<NpgsqlDataSource> ReadModelsAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await ReadModelSchema.EnsureAsync(database, Ct);
        return database;
    }

    /// <summary>Until the end of the session, offset 2 in the in-memory read journal.</summary>
    private static async Task RelayAsync(MemoryJournal journal, NpgsqlDataSource database)
    {
        var runner = new ProjectionRunner(
            journal.ReadJournal,
            database,
            new NpgsqlOffsetStore(database),
            new IntegrationEventRelay(),
            Tag,
            ProjectionRun.QuickBackoff,
            _ => { },
            journal.System.Log);
        await using (runner.Start(journal.System.Materializer()))
        {
            await ProjectionRun.StoredAsync(database, Stream, offset: 2, Ct);
        }
    }

    private static async Task ResetOffsetAsync(NpgsqlDataSource database)
    {
        await using var reset = database.CreateCommand(
            "DELETE FROM read_models.projection_offsets WHERE projection_id = $1");
        reset.Parameters.AddWithValue(IntegrationEventRelay.Id);
        await reset.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<List<(long Position, string Type, string Payload)>> OutboxAsync(
        NpgsqlDataSource database)
    {
        await using var select = database.CreateCommand(
            "SELECT position, type, payload::text FROM read_models.integration_outbox");
        await using var reader = await select.ExecuteReaderAsync(Ct);
        var rows = new List<(long, string, string)>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    private static async Task<long> CountAsync(NpgsqlDataSource database, string table)
    {
        await using var count = database.CreateCommand(
            $"SELECT count(*) FROM read_models.{table}");
        return (long)(await count.ExecuteScalarAsync(Ct))!;
    }
}
