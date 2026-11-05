using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Testing;

namespace Ohmline.Charging.Actors.Tests.Serialization;

/// <summary>
/// Every manifest, through Akka.NET's own serialization, as the journal and the cluster use it:
/// the right serializer is chosen, the message comes back equal, and its JSON matches the golden
/// file, which can itself still be read.
/// </summary>
public sealed partial class SerializationTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Serializers.Charging), nameof(SerializationTests), output)
{
    private static readonly Dictionary<string, object> Samples =
        ChargingSamples.All.ToDictionary(sample => ChargingSerializer.Manifests[sample.GetType()]);

    public static TheoryData<string> Manifests =>
        new(ChargingSerializer.Manifests.Values.Order(StringComparer.Ordinal));

    [Fact]
    public void Every_concrete_type_bound_to_the_serializer_has_a_manifest()
    {
        var bound = ChargingSerializer.BoundTypes
            .SelectMany(root => root.Assembly.GetTypes())
            .Where(type => !type.IsAbstract)
            .Where(type => ChargingSerializer.BoundTypes.Any(root => root.IsAssignableFrom(type)))
            .Distinct();

        ChargingSerializer.Manifests.Keys.ShouldBe(bound, ignoreOrder: true);
    }

    [Fact]
    public void Every_manifest_is_named_by_the_scheme_and_used_once()
    {
        var manifests = ChargingSerializer.Manifests.Values.ToList();

        manifests.ShouldAllBe(manifest => Scheme().IsMatch(manifest));
        manifests.ShouldBeUnique();
    }

    [Fact]
    public void Every_manifest_has_a_sample() =>
        Samples.Keys.ShouldBe(ChargingSerializer.Manifests.Values, ignoreOrder: true);

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Given_a_message_when_serialized_then_the_charging_serializer_writes_it(
        string manifest)
    {
        var sample = Samples[manifest];

        var serializer = Sys.Serialization.FindSerializerFor(sample);

        serializer.ShouldBeOfType<ChargingSerializer>().Identifier.ShouldBe(9101);
        Akka.Serialization.Serialization.ManifestFor(serializer, sample).ShouldBe(manifest);
    }

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Given_a_message_when_it_round_trips_then_it_comes_back_equal(string manifest)
    {
        var sample = Samples[manifest];

        var bytes = Sys.Serialization.FindSerializerFor(sample).ToBinary(sample);

        Sys.Serialization.Deserialize(bytes, 9101, manifest).ShouldBe(sample);
    }

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Given_a_message_when_serialized_then_its_json_matches_the_golden_file(
        string manifest)
    {
        var sample = Samples[manifest];

        var bytes = Sys.Serialization.FindSerializerFor(sample).ToBinary(sample);

        Golden.Verify(manifest, Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Given_the_golden_json_when_read_then_it_gives_the_message_back(string manifest)
    {
        var bytes = Encoding.UTF8.GetBytes(Golden.Read(manifest));

        Sys.Serialization.Deserialize(bytes, 9101, manifest).ShouldBe(Samples[manifest]);
    }

    [Fact]
    public void Given_an_unknown_manifest_when_read_then_it_throws_a_serialization_exception()
    {
        var serializer = new ChargingSerializer((ExtendedActorSystem)Sys);

        Should.Throw<SerializationException>(() =>
                serializer.FromBinary("{}"u8.ToArray(), "charging.connector-teleported.v1"))
            .Message.ShouldContain("charging.connector-teleported.v1");
    }

    [GeneratedRegex(@"^charging\.[a-z0-9]+(-[a-z0-9]+)*\.v[1-9][0-9]*$")]
    private static partial Regex Scheme();
}
