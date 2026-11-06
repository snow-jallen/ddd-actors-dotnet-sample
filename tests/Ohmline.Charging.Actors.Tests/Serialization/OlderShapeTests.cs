using System.Text;
using Akka.TestKit.Xunit;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Testing;

namespace Ohmline.Charging.Actors.Tests.Serialization;

/// <summary>
/// Expand without breaking (section 20.2): a field added to a shape that is already stored is
/// optional, so the JSON written before it existed, frozen in <c>tests/golden/corpus/</c>, still
/// reads under the same manifest, with the field missing.
/// </summary>
public sealed class OlderShapeTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Serializers.Charging), nameof(OlderShapeTests), output)
{
    [Fact]
    public void Given_a_session_started_stored_before_phase_counts_when_read_then_it_has_none()
    {
        var read = Read<SessionStarted>("charging.session-started.v1");

        read.PhaseCount.ShouldBeNull();
        read.ShouldBe(Sample<SessionStarted>() with { PhaseCount = null, PricingModel = null });
    }

    [Fact]
    public void Given_a_start_session_sent_by_an_older_node_when_read_then_it_has_no_phase_count()
    {
        var read = Read<StartSession>("charging.start-session.v1");

        read.ShouldBe(Sample<StartSession>() with { PhaseCount = null });
    }

    [Fact]
    public void Given_a_session_snapshot_stored_before_phase_counts_when_restored_then_3_assumed()
    {
        var read = Read<ChargingSessionSnapshot>("charging.charging-session-snapshot.v1");

        read.ShouldBe(
            Sample<ChargingSessionSnapshot>() with { PhaseCount = null, PricingModel = null });
        var terms = read.ToState().Value.ShouldBeOfType<SuspendedByEv>().Terms;
        terms.PhaseCount.ShouldBe(SessionTerms.AssumedPhaseCount);
        terms.PricingModel.ShouldBe(PricingModel.Standard);
    }

    private T Read<T>(string manifest)
    {
        var bytes = Encoding.UTF8.GetBytes(Golden.Read($"corpus/{manifest}"));
        return Sys.Serialization.Deserialize(bytes, 9101, manifest).ShouldBeOfType<T>();
    }

    private static T Sample<T>() => ChargingSamples.All.OfType<T>().Single();
}
