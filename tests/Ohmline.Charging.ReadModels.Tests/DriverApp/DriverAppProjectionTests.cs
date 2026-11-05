using System.Collections.Immutable;
using System.Text.Json;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.DriverApp;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using static Ohmline.Charging.ReadModels.Tests.Known;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.ReadModels.Tests.DriverApp;

/// <summary>
/// <see cref="DriverAppProjection"/> against PostgreSQL, with envelopes made by hand and a fake
/// for the listings it asks for display names: the cards, the feed and the announcements.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class DriverAppProjectionTests(PostgresFixture postgres)
{
    private const string DepotBays = "Salt Lake bus depot, public bays";

    private static readonly EventEnvelope Started = SessionEnvelope(1, new SessionStarted(
        Session,
        Depot,
        new ConnectorId(Charger, 1),
        DriversCard,
        Tariff,
        new Money(40m, Currency.Usd),
        MeterStartWh: 1_200_000) { OccurredAt = Morning });

    private static readonly EventEnvelope Recorded =
        SessionEnvelope(
            2,
            new MeterValueRecorded(
                Session, Energy.FromWattHours(1_207_350), Morning.AddHours(1)));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_listed_charge_point_then_a_new_sessions_card_has_its_name()
    {
        await using var database = await ReadModelsAsync();
        var names = new Names(DepotBays);

        var announced = await DeliverAsync(database, Started, names);

        var card = (await CardAsync(database)).ShouldNotBeNull();
        card.ChargePointDisplayName.ShouldBe(DepotBays);
        card.Status.ShouldBe("charging");
        var changed = announced.ShouldHaveSingleItem().ShouldBeOfType<DriverCardChanged>();
        changed.TokenUid.ShouldBe(DriversCard);
        changed.ChargePointDisplayName.ShouldBe(DepotBays);
        var feed = await FeedAsync(database);
        feed.ShouldHaveSingleItem().Position.ShouldBe(changed.Position);
        feed[0].Payload.GetProperty("chargePointDisplayName").GetString().ShouldBe(DepotBays);
        feed[0].Payload.TryGetProperty("meterStartWh", out _).ShouldBeFalse();
        names.Asked.ShouldBe([Charger]);
    }

    [Fact]
    public async Task Given_the_same_envelope_twice_then_one_card_and_one_feed_entry_per_version()
    {
        await using var database = await ReadModelsAsync();
        var names = new Names(DepotBays);

        var announced = new List<LiveUpdate>();
        foreach (var envelope in new[] { Started, Recorded, Recorded, Started })
        {
            announced.AddRange(await DeliverAsync(database, envelope, names));
        }

        (await CardAsync(database)).ShouldNotBeNull().Version.ShouldBe(2);
        (await FeedAsync(database)).Select(entry => entry.Version).ShouldBe([1, 2]);
        announced.Cast<DriverCardChanged>().Select(changed => changed.Version).ShouldBe([1, 2]);
        names.Asked.ShouldHaveSingleItem(); // Once, for the new card: it keeps the name.
    }

    [Fact]
    public async Task Given_a_listing_that_did_not_answer_then_the_next_event_asks_it_again()
    {
        await using var database = await ReadModelsAsync();
        var names = new Names(null, DepotBays);

        await DeliverAsync(database, Started, names);
        var nameless = await CardAsync(database);
        await DeliverAsync(database, Recorded, names);

        nameless.ShouldNotBeNull().ChargePointDisplayName.ShouldBeNull();
        names.Asked.ShouldBe([Charger, Charger]);
        var card = (await CardAsync(database)).ShouldNotBeNull();
        card.ChargePointDisplayName.ShouldBe(DepotBays);
        card.EnergyKwh.ShouldBe(7.35m);
    }

    private static EventEnvelope SessionEnvelope(long sequenceNr, SessionEvent @event) =>
        new(
            Offset.Sequence(sequenceNr),
            $"charging-session-{Session}",
            sequenceNr,
            @event,
            0L,
            [ChargingTags.ChargingSession]);

    /// <summary>What the runner does with one envelope, without the offset.</summary>
    private static async Task<ImmutableArray<LiveUpdate>> DeliverAsync(
        NpgsqlDataSource database, EventEnvelope envelope, Names names)
    {
        await using var connection = await database.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        var changes =
            await new DriverAppProjection(names).HandleAsync(envelope, transaction, Ct);
        await transaction.CommitAsync(Ct);
        return changes;
    }

    private static async Task<DriverAppCard?> CardAsync(NpgsqlDataSource database)
    {
        await using var connection = await database.OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        return await DriverAppTables.FindCardAsync(transaction, Session.Value, Ct);
    }

    private static async Task<List<(long Position, long Version, JsonElement Payload)>> FeedAsync(
        NpgsqlDataSource database)
    {
        await using var select = database.CreateCommand(
            "SELECT position, version, payload FROM read_models.driver_app_feed ORDER BY position");
        await using var reader = await select.ExecuteReaderAsync(Ct);
        var feed = new List<(long, long, JsonElement)>();
        while (await reader.ReadAsync(Ct))
        {
            feed.Add((
                reader.GetInt64(0),
                reader.GetInt64(1),
                JsonDocument.Parse(reader.GetString(2)).RootElement));
        }

        return feed;
    }

    private async Task<NpgsqlDataSource> ReadModelsAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        return database;
    }

    /// <summary>
    /// The listings, answering one name per question in turn; the last answer stays. It
    /// remembers which charge points it was asked about.
    /// </summary>
    private sealed class Names(params string?[] answers) : IChargePointNames
    {
        public List<ChargePointId> Asked { get; } = [];

        public Task<string?> DisplayNameAsync(
            ChargePointId chargePointId, CancellationToken cancellationToken)
        {
            Asked.Add(chargePointId);
            return Task.FromResult(answers[Math.Min(Asked.Count, answers.Length) - 1]);
        }
    }
}
