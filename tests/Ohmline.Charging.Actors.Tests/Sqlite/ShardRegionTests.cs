using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Identity;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// The shard regions as Charging's repositories: a command addressed to an aggregate's id
/// reaches that aggregate's one entity, started on demand, which stores its events under the
/// durable persistence id of its type name and its id.
/// </summary>
public sealed class ShardRegionTests
{
    private static readonly ChargePointId NextCharger = new("OHM-SLC-0043");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_two_charge_points_when_registered_then_each_is_an_entity_of_its_own()
    {
        using var database = new TemporaryDatabase();
        await using var node =
            await SqliteNode.StartAsync(database, new FakeTimeProvider(Morning), Ct);

        (await node.AskAsync<CommandReply>(Register(Charger), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<CommandReply>(Register(NextCharger), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<CommandReply>(Reserve(1, DriversCard), Ct))
            .ShouldBe(new CommandAccepted(2));

        (await node.AskAsync<ChargePointStatus>(Status(NextCharger), Ct))
            .Connectors[0].State.ShouldBe(ConnectorStatus.Available);
    }

    [Fact]
    public async Task Given_a_command_through_each_region_then_the_journal_names_its_entity()
    {
        using var database = new TemporaryDatabase();
        await using var node =
            await SqliteNode.StartAsync(database, new FakeTimeProvider(Morning), Ct);

        (await node.AskAsync<ChargePointRegion, CommandReply>(Register(), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<ChargePointListingRegion, CommandReply>(Revise(), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<ChargingSessionRegion, CommandReply>(Begin(), Ct))
            .ShouldBe(new CommandAccepted(1));

        (await PersistenceIdsAsync(database)).ShouldBe(
            [
                "charge-point-OHM-SLC-0042",
                "charge-point-listing-OHM-SLC-0042",
                $"charging-session-{Session}",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Given_a_fake_clock_among_the_services_then_the_entities_decide_by_it()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await SqliteNode.StartAsync(database, clock, Ct);
        await node.AskAsync<CommandReply>(Register(), Ct);
        clock.Advance(TimeSpan.FromMinutes(5));

        (await node.AskAsync<CommandReply>(Reserve(1, DriversCard), Ct))
            .ShouldBe(new CommandAccepted(2));

        (await node.AskAsync<ChargePointStatus>(Status(), Ct))
            .Connectors[0].ReservedUntil.ShouldBe(Morning.AddMinutes(5 + 15));
    }

    private static ReviseChargePointListing Revise() => new(
        Command,
        Charger,
        ChargingSamples.DisplayName,
        ChargingSamples.AccessInstructions,
        ChargingSamples.OpeningHours);

    private static StartSession Begin() => new(
        Command,
        Session,
        Depot,
        new ConnectorId(Charger, 1),
        DriversCard,
        ChargingSamples.Tariff,
        ChargingSamples.SpendCap,
        MeterStartWh: 1_200_000);

    private static async Task<List<string>> PersistenceIdsAsync(TemporaryDatabase database)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var select = connection.CreateCommand();
        select.CommandText = "SELECT DISTINCT persistence_id FROM journal";
        await using var reader = await select.ExecuteReaderAsync(Ct);
        var ids = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }
}
