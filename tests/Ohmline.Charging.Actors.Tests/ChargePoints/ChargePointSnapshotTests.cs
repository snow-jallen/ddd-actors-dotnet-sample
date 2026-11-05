using System.Collections.Immutable;
using Akka.TestKit.Xunit;
using Ohmline.Charging.Actors.ChargePoints;
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
        ChargePointState.Initial.ToSnapshot().ShouldBeNull();

    [Fact]
    public void Given_a_connector_in_every_state_when_read_back_then_the_state_is_equal()
    {
        ChargePointState state = new Registered(Charger, Depot, ImmutableDictionary.CreateRange(
        [
            Connector(1, new Available()),
            Connector(2, new Reserved(DriversCard, Morning.AddMinutes(15))),
            Connector(3, new Occupied(Session)),
            Connector(4, new Faulted("GroundFailure")),
            Connector(5, new Unavailable()),
        ]));
        var snapshot = state.ToSnapshot().ShouldNotBeNull();

        var bytes = Sys.Serialization.FindSerializerFor(snapshot).ToBinary(snapshot);
        var read = Sys.Serialization.Deserialize(bytes, 9101, "charging.charge-point-snapshot.v1");

        read.ShouldBeOfType<ChargePointSnapshot>().ToState().ShouldBe(state);
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
