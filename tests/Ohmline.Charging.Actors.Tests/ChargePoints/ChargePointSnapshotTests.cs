using System.Collections.Immutable;
using System.Text;
using Akka.TestKit.Xunit;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

/// <summary>
/// A snapshot is a cache of the state: written and read back through Charging's serializer, it
/// must give back an equal state, whatever state each connector is in.
/// </summary>
public sealed class ChargePointSnapshotTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Serializers.Charging), nameof(ChargePointSnapshotTests), output)
{
    [Fact]
    public void Given_an_unregistered_charge_point_then_there_is_nothing_to_snapshot() =>
        ChargePointState.Initial.ToSnapshot(Morning).ShouldBeNull();

    [Fact]
    public void Given_a_connector_in_every_state_when_read_back_then_the_state_is_equal()
    {
        ChargePointState state = new Registered(Charger, Depot, ImmutableDictionary.CreateRange(
        [
            Connector(1, new Available()),
            Connector(2, new Reserved(DriversCard, Morning.AddMinutes(15))),
            Connector(3, new Occupied(Session)),
            Connector(4, new OutOfOrder("GroundFailure")),
            Connector(5, new Unavailable()),
        ]));
        var snapshot = state.ToSnapshot(Morning.AddMinutes(5)).ShouldNotBeNull();

        var bytes = Sys.Serialization.FindSerializerFor(snapshot).ToBinary(snapshot);
        var read = Sys.Serialization.Deserialize(bytes, 9101, "charging.charge-point-snapshot.v2");

        read.ShouldBeOfType<ChargePointSnapshot>().ToState().ShouldBe(state);
    }

    /// <summary>
    /// The second version (section 20.6): a reservation is kept as how long it had left when
    /// the snapshot was taken, and comes back with the same expiry.
    /// </summary>
    [Fact]
    public void Given_a_reservation_when_snapshotted_then_it_is_kept_as_the_time_it_had_left()
    {
        ChargePointState state = new Registered(Charger, Depot, ImmutableDictionary.CreateRange(
            [Connector(1, new Reserved(DriversCard, Morning.AddMinutes(15)))]));

        var snapshot = state.ToSnapshot(Morning.AddMinutes(5)).ShouldNotBeNull();

        snapshot.TakenAt.ShouldBe(Morning.AddMinutes(5));
        snapshot.Connectors.ShouldBe(
        [
            new ConnectorSnapshot.Reserved(
                1, new Power(22m), DriversCard, TimeSpan.FromMinutes(10)),
        ]);
        snapshot.ToState().ShouldBe(state);
    }

    /// <summary>
    /// A snapshot stored in the first version, with each reservation's expiry time, is read
    /// through its upcaster and gives the charge point back as it was (section 20.6).
    /// </summary>
    [Fact]
    public void Given_the_frozen_v1_snapshot_when_read_then_each_connector_is_as_it_was()
    {
        var v1 = Encoding.UTF8.GetBytes(Golden.Read("corpus/charging.charge-point-snapshot.v1"));

        var read = Sys.Serialization.Deserialize(v1, 9101, "charging.charge-point-snapshot.v1");

        var connectors = read.ShouldBeOfType<ChargePointSnapshot>().ToState().Value
            .ShouldBeOfType<Registered>().Connectors;
        connectors.Select(pair => (pair.Key, pair.Value.State)).OrderBy(pair => pair.Key)
            .ShouldBe(
            [
                (1, new Available()),
                (2, new Reserved(ChargingSamples.Token, ChargingSamples.At.AddMinutes(15))),
                (3, new Occupied(ChargingSamples.Session)),
                (4, new OutOfOrder("GroundFailure")),
                (5, new Unavailable()),
            ]);
    }

    /// <summary>
    /// The window of recent commands was added to the snapshot in section 12.6 as an optional
    /// field, under the same manifest: a snapshot stored before it reads with an empty window.
    /// </summary>
    [Fact]
    public void Given_a_snapshot_stored_before_the_window_existed_then_it_reads_with_none()
    {
        var before = """
            {"chargePointId":"OHM-SLC-0042","siteId":"slc-depot","connectors":[
              {"$kind":"available","number":1,"maxPower":22}]}
            """u8.ToArray();

        var read = Sys.Serialization.Deserialize(before, 9101, "charging.charge-point-snapshot.v1");

        var snapshot = read.ShouldBeOfType<ChargePointSnapshot>();
        snapshot.Connectors.ShouldHaveSingleItem();
        snapshot.RecentCommands.ShouldBeEmpty();
    }

    private static KeyValuePair<int, Connector> Connector(int number, ConnectorState state) =>
        new(number, new Connector(new ConnectorId(Charger, number), new Power(22m), state));
}
