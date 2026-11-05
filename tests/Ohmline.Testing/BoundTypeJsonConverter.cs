using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ohmline.Testing;

/// <summary>
/// With <c>serialize-messages = on</c>, Akka.Persistence's own journal messages (the write
/// results its <c>AsyncWriteJournal</c> passes between its internal actors) are copied with
/// Akka.NET's default Newtonsoft.Json serializer, events and all. A domain type that Newtonsoft
/// cannot rebuild, such as a value object with only a private constructor, then fails every
/// actor test, although our own serializer writes it fine. This converter makes Newtonsoft hand
/// any object whose type is bound to another serializer to that serializer, so the copy goes
/// through the same manifest serializer the journal uses.
/// </summary>
internal sealed class BoundTypeJsonConverter(Func<ExtendedActorSystem?> system) : JsonConverter
{
    private readonly ConcurrentDictionary<Type, bool> _bound = new();

    public override bool CanConvert(Type objectType) =>
        system() is { } actorSystem && _bound.GetOrAdd(objectType, type =>
            type != typeof(object)
            && !type.IsPrimitive
            && actorSystem.Serialization.FindSerializerForType(type)
                is not NewtonSoftJsonSerializer);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var actorSystem = system()!;
        var bound = actorSystem.Serialization.FindSerializerFor(value);
        new JObject
        {
            ["$type"] = $"{value!.GetType().FullName}, {value.GetType().Assembly.GetName().Name}",
            ["serializer"] = bound.Identifier,
            ["manifest"] = Akka.Serialization.Serialization.ManifestFor(bound, value),
            ["bytes"] = bound.ToBinary(value),
        }.WriteTo(writer);
    }

    public override object? ReadJson(
        JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var copy = JObject.Load(reader);
        return system()!.Serialization.Deserialize(
            copy["bytes"]!.ToObject<byte[]>()!,
            copy["serializer"]!.Value<int>(),
            copy["manifest"]!.Value<string>()!);
    }
}
