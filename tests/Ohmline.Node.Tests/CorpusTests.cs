using System.Collections.Frozen;
using System.Text;
using Akka.Actor;
using Akka.Serialization;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.DriverAccounts.Actors.Serialization;
using Ohmline.SiteEnergy.Actors.Serialization;
using Ohmline.Tariffs.Actors.Serialization;
using Ohmline.Testing;

namespace Ohmline.Node.Tests;

/// <summary>
/// The golden corpus (section 20.5): the JSON of every manifest the node has ever written,
/// frozen in <c>tests/golden/corpus/</c> as it was first stored and never edited, read on every
/// build by the serializers the node runs today. The golden files beside it say what is written
/// now; the corpus says what must still be read.
/// </summary>
public sealed class CorpusTests : IDisposable
{
    /// <summary>The node's serializers, by their manifests' context.</summary>
    private static readonly FrozenDictionary<string, ContextSerializer> ByContext =
        new Dictionary<string, ContextSerializer>
        {
            ["charging"] = new(9101, ChargingSerializer.BoundTypes, ChargingSerializer.Manifests),
            ["site-energy"] =
                new(9102, SiteEnergySerializer.BoundTypes, SiteEnergySerializer.Manifests),
            ["accounts"] = new(
                9103, DriverAccountsSerializer.BoundTypes, DriverAccountsSerializer.Manifests),
            ["tariffs"] = new(9104, TariffsSerializer.BoundTypes, TariffsSerializer.Manifests),
        }.ToFrozenDictionary();

    private readonly ActorSystem _system = ActorSystem.Create(
        "corpus",
        ActorTestConfig.With(
            system => Details(
                ChargingSerializer.Name, new ChargingSerializer(system), ByContext["charging"]),
            system => Details(
                SiteEnergySerializer.Name,
                new SiteEnergySerializer(system),
                ByContext["site-energy"]),
            system => Details(
                DriverAccountsSerializer.Name,
                new DriverAccountsSerializer(system),
                ByContext["accounts"]),
            system => Details(
                TariffsSerializer.Name, new TariffsSerializer(system), ByContext["tariffs"]),
            system => SerializerDetails.Create(
                TraceEnvelopeSerializer.Name,
                new TraceEnvelopeSerializer(system),
                [typeof(TraceEnvelope)])));

    private static readonly DirectoryInfo Corpus =
        new(Path.Combine(Golden.Folder.FullName, "corpus"));

    public static TheoryData<string> CorpusFiles =>
        new(Corpus.GetFiles("*.json").Select(file => Path.GetFileNameWithoutExtension(file.Name))
            .Order(StringComparer.Ordinal));

    /// <summary>
    /// Every manifest the node reads today, with the type it reads it as: the ones it writes,
    /// the older versions it upcasts, and the trace envelope.
    /// </summary>
    private static FrozenDictionary<string, Type> ReadToday { get; } =
        ByContext.Values
            .SelectMany(serializer => serializer.Manifests)
            .Select(pair => (Manifest: pair.Value, Type: pair.Key))
            .Concat(ChargingSerializer.Upcasters
                .Select(pair => (Manifest: pair.Key, Type: pair.Value.To)))
            .Append((
                Manifest: TraceEnvelopeSerializer.EnvelopeManifest,
                Type: typeof(TraceEnvelope)))
            .ToFrozenDictionary(read => read.Manifest, read => read.Type);

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void Every_file_in_the_corpus_is_still_read(string manifest)
    {
        var bytes = Encoding.UTF8.GetBytes(Golden.Read($"corpus/{manifest}"));

        var read = _system.Serialization.Deserialize(bytes, IdentifierOf(manifest), manifest);

        read.ShouldBeOfType(ReadToday[manifest]);
    }

    [Fact]
    public void Every_manifest_the_node_reads_has_its_first_stored_json_in_the_corpus()
    {
        var frozen = CorpusFiles.Select(row => row.Data).ToHashSet();

        ReadToday.Keys.Where(manifest => !frozen.Contains(manifest)).ShouldBeEmpty(
            "Freeze the first JSON of each new manifest in tests/golden/corpus/ when it ships.");
    }

    [Fact]
    public void Every_manifest_in_the_corpus_is_still_one_the_node_reads()
    {
        CorpusFiles.Select(row => row.Data).Where(manifest => !ReadToday.ContainsKey(manifest))
            .ShouldBeEmpty("A manifest that was ever stored is read for ever (ADR 0007).");
    }

    /// <summary>
    /// A type that derives from a persisted or sent base type reaches its context's serializer,
    /// which refuses any type without a manifest; so every one of them has a manifest.
    /// </summary>
    [Fact]
    public void Every_type_derived_from_a_persisted_base_type_is_registered()
    {
        var missing = ByContext.Values.SelectMany(serializer => serializer.BoundTypes
                .SelectMany(root => root.Assembly.GetTypes())
                .Where(type => !type.IsAbstract)
                .Where(type => serializer.BoundTypes.Any(root => root.IsAssignableFrom(type)))
                .Where(type => !serializer.Manifests.ContainsKey(type)))
            .Select(type => type.FullName);

        missing.ShouldBeEmpty();
    }

    public void Dispose() => _system.Terminate().Wait(TimeSpan.FromSeconds(10));

    private static int IdentifierOf(string manifest) =>
        manifest == TraceEnvelopeSerializer.EnvelopeManifest
            ? TraceEnvelopeSerializer.Id
            : ByContext[manifest[..manifest.IndexOf('.', StringComparison.Ordinal)]].Identifier;

    private static SerializerDetails Details(
        string name, Serializer serializer, ContextSerializer known) =>
        SerializerDetails.Create(name, serializer, [.. known.BoundTypes]);

    /// <summary>What the corpus checks need to know of one of the node's serializers.</summary>
    private sealed record ContextSerializer(
        int Identifier,
        IReadOnlyList<Type> BoundTypes,
        IReadOnlyDictionary<Type, string> Manifests);
}
