using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.Tariffs.Actors.Serialization;
using Ohmline.Testing;

namespace Ohmline.Tariffs.Actors.Tests.Serialization;

/// <summary>
/// Every Tariffs manifest through Akka.NET's own serialization: the right serializer, an
/// equal message back, and JSON that matches its golden file, which still reads.
/// </summary>
public sealed partial class SerializationTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Serializers.Tariffs), nameof(SerializationTests), output)
{
    private static readonly Dictionary<string, object> Samples =
        TariffsSamples.All.ToDictionary(
            sample => TariffsSerializer.Manifests[sample.GetType()]);

    public static TheoryData<string> Manifests =>
        new(TariffsSerializer.Manifests.Values.Order(StringComparer.Ordinal));

    [Fact]
    public void Every_concrete_type_bound_to_the_serializer_has_a_manifest()
    {
        var bound = TariffsSerializer.BoundTypes
            .SelectMany(root => root.Assembly.GetTypes())
            .Where(type => !type.IsAbstract)
            .Where(type => TariffsSerializer.BoundTypes.Any(root => root.IsAssignableFrom(type)))
            .Distinct();

        TariffsSerializer.Manifests.Keys.ShouldBe(bound, ignoreOrder: true);
    }

    [Fact]
    public void Every_manifest_is_named_by_the_scheme_and_used_once()
    {
        var manifests = TariffsSerializer.Manifests.Values.ToList();

        manifests.ShouldAllBe(manifest => Scheme().IsMatch(manifest));
        manifests.ShouldBeUnique();
    }

    [Fact]
    public void Every_manifest_has_a_sample() =>
        Samples.Keys.ShouldBe(TariffsSerializer.Manifests.Values, ignoreOrder: true);

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Given_a_message_when_serialized_then_the_tariffs_serializer_writes_it(
        string manifest)
    {
        var sample = Samples[manifest];

        var serializer = Sys.Serialization.FindSerializerFor(sample);

        serializer.ShouldBeOfType<TariffsSerializer>().Identifier.ShouldBe(9104);
        Akka.Serialization.Serialization.ManifestFor(serializer, sample).ShouldBe(manifest);
    }

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Given_a_message_when_it_round_trips_then_it_comes_back_equal(string manifest)
    {
        var sample = Samples[manifest];

        var bytes = Sys.Serialization.FindSerializerFor(sample).ToBinary(sample);

        Sys.Serialization.Deserialize(bytes, 9104, manifest).ShouldBe(sample);
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

        Sys.Serialization.Deserialize(bytes, 9104, manifest).ShouldBe(Samples[manifest]);
    }

    [Fact]
    public void Given_an_unknown_manifest_when_read_then_it_throws_a_serialization_exception()
    {
        var serializer = new TariffsSerializer((ExtendedActorSystem)Sys);

        Should.Throw<SerializationException>(() =>
            serializer.FromBinary("{}"u8.ToArray(), "tariffs.free-charging.v1"));
    }

    [GeneratedRegex(@"^tariffs\.[a-z0-9]+(-[a-z0-9]+)*\.v[1-9][0-9]*$")]
    private static partial Regex Scheme();
}
