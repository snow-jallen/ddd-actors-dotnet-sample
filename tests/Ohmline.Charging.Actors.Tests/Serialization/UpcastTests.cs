using System.Text;
using Akka.TestKit.Xunit;
using Ohmline.Charging.Actors.Persistence;
using Ohmline.Charging.Actors.Persistence.Legacy;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Units;
using Ohmline.Testing;

namespace Ohmline.Charging.Actors.Tests.Serialization;

/// <summary>
/// The alternative to section 20.3: the serializer reads <c>charging.meter-value-recorded.v1</c>
/// as its own type, and only the journal's read adapter turns it into today's reading.
/// </summary>
public sealed class UpcastTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Serializers.Charging), nameof(UpcastTests), output)
{
    [Fact]
    public void Given_the_frozen_v1_bytes_then_the_serializer_reads_the_old_type()
    {
        var bytes = Encoding.UTF8.GetBytes(Golden.Read("corpus/charging.meter-value-recorded.v1"));

        Sys.Serialization.Deserialize(bytes, 9101, "charging.meter-value-recorded.v1")
            .ShouldBeOfType<MeterValueRecordedV1>().RegisterWh.ShouldBe(18_210_350);
    }

    [Fact]
    public void Given_the_frozen_v1_bytes_then_the_read_adapter_gives_wh_as_kwh()
    {
        var bytes = Encoding.UTF8.GetBytes(Golden.Read("corpus/charging.meter-value-recorded.v1"));
        var old = Sys.Serialization.Deserialize(bytes, 9101, "charging.meter-value-recorded.v1");

        var events = new LegacyEventsReadAdapter()
            .FromJournal(old, "charging.meter-value-recorded.v1").Events;

        events.ShouldBe(
        [
            new MeterValueRecorded(
                ChargingSamples.Session,
                new Energy(18_210.350m),
                ChargingSamples.At.AddMinutes(10),
                Phase: null)
            {
                CommandId = ChargingSamples.Command,
                OccurredAt = ChargingSamples.At,
            },
        ]);
    }

    [Fact]
    public void Given_a_reading_when_written_then_it_is_written_as_v2()
    {
        var reading = ChargingSamples.All.OfType<MeterValueRecorded>().Single();

        var serializer = Sys.Serialization.FindSerializerFor(reading);

        Akka.Serialization.Serialization.ManifestFor(serializer, reading)
            .ShouldBe("charging.meter-value-recorded.v2");
    }
}
