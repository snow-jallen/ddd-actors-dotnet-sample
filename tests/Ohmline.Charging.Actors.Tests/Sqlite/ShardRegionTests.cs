using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Sessions;
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

    [Fact]
    public async Task Given_an_ended_session_then_its_shard_stops_it_until_the_next_command()
    {
        using var database = new TemporaryDatabase();
        await using var node =
            await SqliteNode.StartAsync(database, new FakeTimeProvider(Morning), Ct);
        var probe = new TestProbe(node.System, new XunitAssertions());
        node.System.EventStream.Subscribe(probe.Ref, typeof(Info));
        (await node.AskAsync<ChargingSessionRegion, CommandReply>(Begin(), Ct))
            .ShouldBe(new CommandAccepted(1));
        var entity = await EntityAsync(node.System, probe);
        await probe.WatchAsync(entity);

        (await node.AskAsync<ChargingSessionRegion, CommandReply>(End(), Ct))
            .ShouldBe(new CommandAccepted(2));
        await probe.FishForMessageAsync<Terminated>(
            terminated => terminated.ActorRef.Equals(entity), cancellationToken: Ct);

        // A late retry of the stop starts the session again from its journal.
        (await node.AskAsync<ChargingSessionRegion, CommandReply>(End(), Ct))
            .ShouldBe(new CommandAccepted(2));
        await probe.FishForMessageAsync<Info>(
            info => $"{info.Message}".StartsWith(
                $"charging-session-{Session} recovered to sequence number 2 in "),
            cancellationToken: Ct);
    }

    /// <summary>The session's entity: a child of its shard, under the region's type name.</summary>
    private static async Task<IActorRef> EntityAsync(ActorSystem system, TestProbe probe)
    {
        system.ActorSelection($"/system/sharding/{ChargingSessionActor.TypeName}/*/{Session}")
            .Tell(new Identify(Session), probe.Ref);
        var identity = await probe.FishForMessageAsync<ActorIdentity>(
            _ => true, cancellationToken: Ct);
        return identity.Subject.ShouldNotBeNull();
    }

    private static StopSession End() => new(Command, Session);

    private static ReviseChargePointListing Revise() => new(
        Command,
        Charger,
        ChargingSamples.DisplayName,
        ChargingSamples.AccessInstructions,
        ChargingSamples.OpeningHours);

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
