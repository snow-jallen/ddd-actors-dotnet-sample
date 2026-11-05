using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Akka.Actor;
using Akka.TestKit.Xunit;
using Ohmline.DriverAccounts.Actors.Serialization;
using Ohmline.Testing;

namespace Ohmline.DriverAccounts.Actors.Tests.Serialization;

/// <summary>
/// Every Driver Accounts manifest through Akka.NET's own serialization: the right serializer, an
/// equal message back, and JSON that matches its golden file, which still reads.
/// </summary>
public sealed partial class SerializationTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(Serializers.DriverAccounts), nameof(SerializationTests), output)
{
    private static readonly Dictionary<string, object> Samples =
        DriverAccountsSamples.All.ToDictionary(
            sample => DriverAccountsSerializer.Manifests[sample.GetType()]);

    public static TheoryData<string> Manifests =>
        new(DriverAccountsSerializer.Manifests.Values.Order(StringComparer.Ordinal));

    [Fact]
    public void Every_concrete_type_bound_to_the_serializer_has_a_manifest()
    {
        var bound = DriverAccountsSerializer.BoundTypes
            .SelectMany(root => root.Assembly.GetTypes())
            .Where(type => !type.IsAbstract)
            .Where(type =>
                DriverAccountsSerializer.BoundTypes.Any(root => root.IsAssignableFrom(type)))
            .Distinct();

        DriverAccountsSerializer.Manifests.Keys.ShouldBe(bound, ignoreOrder: true);
    }

    [Fact]
    public void Every_manifest_is_named_by_the_scheme_and_used_once()
    {
        var manifests = DriverAccountsSerializer.Manifests.Values.ToList();

        manifests.ShouldAllBe(manifest => Scheme().IsMatch(manifest));
        manifests.ShouldBeUnique();
    }

    [Fact]
    public void Every_manifest_has_a_sample() =>
        Samples.Keys.ShouldBe(DriverAccountsSerializer.Manifests.Values, ignoreOrder: true);

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Given_a_message_when_serialized_then_the_driver_accounts_serializer_writes_it(
        string manifest)
    {
        var sample = Samples[manifest];

        var serializer = Sys.Serialization.FindSerializerFor(sample);

        serializer.ShouldBeOfType<DriverAccountsSerializer>().Identifier.ShouldBe(9103);
        Akka.Serialization.Serialization.ManifestFor(serializer, sample).ShouldBe(manifest);
    }

    [Theory]
    [MemberData(nameof(Manifests))]
    public void Given_a_message_when_it_round_trips_then_it_comes_back_equal(string manifest)
    {
        var sample = Samples[manifest];

        var bytes = Sys.Serialization.FindSerializerFor(sample).ToBinary(sample);

        Sys.Serialization.Deserialize(bytes, 9103, manifest).ShouldBe(sample);
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

        Sys.Serialization.Deserialize(bytes, 9103, manifest).ShouldBe(Samples[manifest]);
    }

    [Fact]
    public void Given_an_unknown_manifest_when_read_then_it_throws_a_serialization_exception()
    {
        var serializer = new DriverAccountsSerializer((ExtendedActorSystem)Sys);

        Should.Throw<SerializationException>(() =>
            serializer.FromBinary("{}"u8.ToArray(), "accounts.card-cloned.v1"));
    }

    [GeneratedRegex(@"^accounts\.[a-z0-9]+(-[a-z0-9]+)*\.v[1-9][0-9]*$")]
    private static partial Regex Scheme();
}
