using Akka.TestKit.Xunit;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Domain.Metering;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Units;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sessions;

/// <summary>A session's snapshot, written and read back, gives back an equal state.</summary>
public sealed class ChargingSessionSnapshotTests(ITestOutputHelper output)
    : TestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(ChargingSessionSnapshotTests), output)
{
    private static readonly SessionTerms Terms = new(
        Session,
        Depot,
        ChargingSamples.Connector,
        DriversCard,
        ChargingSamples.Tariff,
        ChargingSamples.SpendCap);

    private static readonly SessionMeter Meter = new(
        MeterReading.FromWattHours(18_204_000, Morning),
        MeterReading.FromWattHours(18_210_350, Morning.AddMinutes(10)));

    [Fact]
    public void Given_no_session_then_there_is_nothing_to_snapshot() =>
        SessionState.Initial.ToSnapshot().ShouldBeNull();

    [Fact]
    public void Given_a_session_in_any_phase_when_read_back_then_the_state_is_equal()
    {
        SessionState[] states =
        [
            new Active(Terms, Meter),
            new SuspendedByEv(Terms, Meter, Morning.AddMinutes(12)),
            new SuspendedByCharger(Terms, Meter, Morning.AddMinutes(12)),
            new Ended(Terms, new Energy(6.35m), Morning.AddMinutes(30)),
        ];

        foreach (var state in states)
        {
            var snapshot = state.ToSnapshot().ShouldNotBeNull();
            var bytes = Sys.Serialization.FindSerializerFor(snapshot).ToBinary(snapshot);
            var read = Sys.Serialization.Deserialize(
                bytes, 9101, "charging.charging-session-snapshot.v1");

            read.ShouldBeOfType<ChargingSessionSnapshot>().ToState().ShouldBe(state);
        }
    }
}
