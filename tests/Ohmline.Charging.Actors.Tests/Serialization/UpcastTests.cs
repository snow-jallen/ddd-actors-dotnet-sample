using System.Text;
using System.Text.RegularExpressions;
using Akka.TestKit.Xunit;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Units;
using Ohmline.Testing;

namespace Ohmline.Charging.Actors.Tests.Serialization;

/// <summary>
/// Upcasting inside the serializer (section 20.3): a manifest Charging no longer writes is read
/// from its frozen bytes in <c>tests/golden/corpus/</c>, as the shape it was written in, and
/// comes back as the type written today.
/// </summary>
public sealed partial class UpcastTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Serializers.Charging), nameof(UpcastTests), output)
{
    public static TheoryData<string> Upcast => new(ChargingSerializer.Upcasters.Keys.Order());

    public static TheoryData<string> NewVersions => new(ChargingSerializer.Upcasters
        .Where(pair => pair.Value.From != pair.Value.To)
        .Select(pair => pair.Key)
        .Order());

    public static TheoryData<string> Renamed => new(ChargingSerializer.Upcasters
        .Where(pair => pair.Value.From == pair.Value.To)
        .Select(pair => pair.Key)
        .Order());

    [Fact]
    public void Given_the_frozen_v1_bytes_of_a_reading_then_its_wh_are_read_as_kwh()
    {
        var bytes = Encoding.UTF8.GetBytes(Golden.Read("corpus/charging.meter-value-recorded.v1"));

        var read = Sys.Serialization.Deserialize(bytes, 9101, "charging.meter-value-recorded.v1");

        read.ShouldBe(
            new MeterValueRecorded(
                ChargingSamples.Session,
                new Energy(18_210.350m),
                ChargingSamples.At.AddMinutes(10),
                Phase: null)
            {
                CommandId = ChargingSamples.Command,
                OccurredAt = ChargingSamples.At,
            });
    }

    [Fact]
    public void Given_a_reading_when_written_then_it_is_written_as_v2()
    {
        var reading = ChargingSamples.All.OfType<MeterValueRecorded>().Single();

        var serializer = Sys.Serialization.FindSerializerFor(reading);

        Akka.Serialization.Serialization.ManifestFor(serializer, reading)
            .ShouldBe("charging.meter-value-recorded.v2");
    }

    [Theory]
    [MemberData(nameof(NewVersions))]
    public void Every_upcast_manifest_is_an_older_version_of_one_written_today(string manifest)
    {
        var upcaster = ChargingSerializer.Upcasters[manifest];
        var older = Versioned().Match(manifest);
        var today = Versioned().Match(ChargingSerializer.Manifests[upcaster.To]);

        older.Success.ShouldBeTrue();
        today.Groups["name"].Value.ShouldBe(older.Groups["name"].Value);
        int.Parse(today.Groups["version"].Value)
            .ShouldBeGreaterThan(int.Parse(older.Groups["version"].Value));
    }

    [Theory]
    [MemberData(nameof(Upcast))]
    public void Every_upcast_manifest_reads_its_frozen_bytes_as_today_s_type(string manifest)
    {
        var bytes = Encoding.UTF8.GetBytes(Golden.Read($"corpus/{manifest}"));

        Sys.Serialization.Deserialize(bytes, 9101, manifest)
            .ShouldBeOfType(ChargingSerializer.Upcasters[manifest].To);
    }

    /// <summary>
    /// A type renamed under a new manifest (section 20.7) is written under the new one and read
    /// under both: the old manifest is an alias, never written again.
    /// </summary>
    [Theory]
    [MemberData(nameof(Renamed))]
    public void Every_renamed_manifest_is_read_as_the_type_that_now_has_another(string manifest)
    {
        var renamed = ChargingSerializer.Upcasters[manifest].To;

        ChargingSerializer.Manifests[renamed].ShouldNotBe(manifest);
        ChargingSerializer.Manifests.Values.ShouldNotContain(manifest);
    }

    [GeneratedRegex(@"^(?<name>charging\.[a-z0-9-]+)\.v(?<version>[1-9][0-9]*)$")]
    private static partial Regex Versioned();
}
