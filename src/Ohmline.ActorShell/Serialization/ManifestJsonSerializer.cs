using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Akka.Actor;
using Akka.Serialization;

namespace Ohmline.ActorShell.Serialization;

/// <summary>
/// The base of every context's serializer: System.Text.Json under an explicit manifest per type.
/// The journal stores the serializer's identifier and the manifest beside every event, and never
/// a CLR type name, so a type can be renamed or moved without breaking replay. Changing a
/// manifest or reusing an identifier is what breaks it (ADR 0007). A subclass supplies its
/// identifier and its registry, written out by hand and never derived from type names; and the
/// older manifest versions it still reads, each with its upcaster (section 20.3).
/// </summary>
public abstract class ManifestJsonSerializer : SerializerWithStringManifest
{
    private readonly FrozenDictionary<Type, string> _manifests;
    private readonly FrozenDictionary<string, Type> _types;
    private readonly FrozenDictionary<string, Upcaster> _upcasters;
    private readonly JsonSerializerOptions _options;

    /// <param name="system">The actor system that owns the serializer.</param>
    /// <param name="manifests">Every type this serializer writes, with its manifest.</param>
    /// <param name="configure">Options a context adds, such as converters of its own.</param>
    protected ManifestJsonSerializer(
        ExtendedActorSystem system,
        IReadOnlyDictionary<Type, string> manifests,
        Action<JsonSerializerOptions>? configure = null)
        : this(system, manifests, new Dictionary<string, Upcaster>(), configure)
    {
    }

    /// <param name="system">The actor system that owns the serializer.</param>
    /// <param name="manifests">Every type this serializer writes, with its manifest.</param>
    /// <param name="upcasters">
    /// Manifests it no longer writes but reads for ever, each with the upcaster that turns the
    /// stored shape into a type it writes today.
    /// </param>
    /// <param name="configure">Options a context adds, such as converters of its own.</param>
    protected ManifestJsonSerializer(
        ExtendedActorSystem system,
        IReadOnlyDictionary<Type, string> manifests,
        IReadOnlyDictionary<string, Upcaster> upcasters,
        Action<JsonSerializerOptions>? configure = null)
        : base(system)
    {
        _manifests = manifests.ToFrozenDictionary();
        _types = manifests
            .ToDictionary(pair => pair.Value, pair => pair.Key) // Throws on a manifest used twice.
            .ToFrozenDictionary();
        _upcasters = upcasters.ToFrozenDictionary();
        if (_upcasters.Keys.FirstOrDefault(_types.ContainsKey) is { } both)
        {
            throw new ArgumentException(
                $"'{both}' is written and upcast at once: a manifest is one or the other.",
                nameof(upcasters));
        }

        if (_upcasters.FirstOrDefault(pair => !_manifests.ContainsKey(pair.Value.To)) is
            { Key: { } stray, Value: var upcaster })
        {
            throw new ArgumentException(
                $"'{stray}' is upcast to {upcaster.To}, which has no manifest of its own.",
                nameof(upcasters));
        }

        _options = CreateOptions(configure);
    }

    public override string Manifest(object o) =>
        _manifests.TryGetValue(o.GetType(), out var manifest)
            ? manifest
            : throw new ArgumentException(
                $"{GetType().Name} has no manifest for {o.GetType()}. Add one to its registry.",
                nameof(o));

    public override byte[] ToBinary(object obj)
    {
        _ = Manifest(obj);
        return JsonSerializer.SerializeToUtf8Bytes(obj, obj.GetType(), _options);
    }

    /// <summary>
    /// Reads the bytes as the type registered for the manifest, or, for an older version, as the
    /// shape it was written in, upcast to today's type. An unknown manifest throws a
    /// <see cref="SerializationException"/>, as Akka.NET asks: it may be a newer node's message.
    /// It is an <see cref="UnknownManifestException"/>, which keeps the manifest (section 11.3).
    /// </summary>
    public override object FromBinary(byte[] bytes, string manifest)
    {
        if (_types.TryGetValue(manifest, out var type))
        {
            return Read(bytes, type, manifest);
        }

        if (_upcasters.TryGetValue(manifest, out var upcaster))
        {
            return upcaster.Upcast(Read(bytes, upcaster.From, manifest));
        }

        throw new UnknownManifestException(GetType().Name, Identifier, manifest);
    }

    private object Read(byte[] bytes, Type type, string manifest) =>
        JsonSerializer.Deserialize(bytes, type, _options)
            ?? throw new SerializationException($"The '{manifest}' payload is null.");

    /// <summary>
    /// The tolerant reader's rules (section 20.2). A member the type does not know is skipped,
    /// so an older node reads a newer shape. A member the JSON lacks is refused, unless its
    /// constructor parameter has a default: so a field added later must be optional, with a
    /// default, and then a shape written before it existed still reads.
    /// </summary>
    private static JsonSerializerOptions CreateOptions(Action<JsonSerializerOptions>? configure)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters = { new ValueObjectConverter(), new JsonStringEnumConverter() },
        };
        configure?.Invoke(options);
        options.MakeReadOnly();
        return options;
    }
}
