using System.Collections.Immutable;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

public sealed class ChargePointStateTests
{
    [Fact]
    public void Given_a_new_charge_point_then_it_starts_unregistered()
    {
        ChargePointState.Initial.Value.ShouldBeOfType<Unregistered>();
    }

    [Fact]
    public void Given_a_default_state_then_it_holds_no_case_and_only_a_null_arm_catches_it()
    {
        ChargePointState state = default;

        state.Value.ShouldBeNull();
        Describe(state).ShouldBe("no state at all: start from ChargePointState.Initial");
    }

    [Fact]
    public void Given_each_connector_state_then_it_carries_only_its_own_data()
    {
        var expiry = Morning.AddMinutes(15);

        Describe(new Available()).ShouldBe("available");
        Describe(new Reserved(DriversCard, expiry))
            .ShouldBe("reserved for 04A2B3C4D5E6F7 until 08:07");
        Describe(new Occupied(Session)).ShouldBe($"occupied by session {Session}");
        Describe(new Faulted("GroundFailure")).ShouldBe("faulted: GroundFailure");
        Describe(new Unavailable()).ShouldBe("unavailable");
        Describe(default(ConnectorState)).ShouldBe("unknown");
    }

    [Fact]
    public void Given_a_reservation_then_it_protects_the_connector_until_its_expiry_time()
    {
        var reservation = new Reserved(DriversCard, Morning.AddMinutes(15));

        reservation.HasExpiredAt(Morning.AddMinutes(15).AddTicks(-1)).ShouldBeFalse();
        reservation.HasExpiredAt(Morning.AddMinutes(15)).ShouldBeTrue();
    }

    [Fact]
    public void Given_two_registered_states_with_the_same_connectors_then_they_are_equal()
    {
        var one = Registered(Connector(1, new Available()), Connector(2, new Occupied(Session)));
        var other = Registered(Connector(2, new Occupied(Session)), Connector(1, new Available()));

        other.ShouldBe(one);
        other.GetHashCode().ShouldBe(one.GetHashCode());
        ((ChargePointState)other).ShouldBe(one);
    }

    [Fact]
    public void Given_registered_states_whose_connectors_differ_then_they_differ()
    {
        var available = Registered(Connector(1, new Available()));

        Registered(Connector(1, new Unavailable())).ShouldNotBe(available);
        Registered(Connector(1, new Available()), Connector(2, new Available()))
            .ShouldNotBe(available);
    }

    private static Registered Registered(params Connector[] connectors) =>
        new(ChargePoint, Depot, connectors.ToImmutableDictionary(connector => connector.Id.Number));

    private static Connector Connector(int number, ConnectorState state) =>
        new(new ConnectorId(ChargePoint, number), new Power(50m), state);

    private static string Describe(ChargePointState state) => state switch
    {
        Unregistered => "unregistered",
        Registered registered => $"{registered.Connectors.Count} connectors",
        null => "no state at all: start from ChargePointState.Initial",
    };

    private static string Describe(ConnectorState state) => state switch
    {
        Available => "available",
        Reserved reserved => $"reserved for {reserved.TokenUid} until {reserved.ExpiresAt:HH:mm}",
        Occupied occupied => $"occupied by session {occupied.SessionId}",
        Faulted faulted => $"faulted: {faulted.ErrorCode}",
        Unavailable => "unavailable",
        null => "unknown",
    };
}
