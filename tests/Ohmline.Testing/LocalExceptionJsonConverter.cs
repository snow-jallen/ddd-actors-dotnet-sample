using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ohmline.Testing;

/// <summary>
/// With <c>serialize-messages = on</c>, Akka.Persistence's journal protocol carries exceptions
/// between the journal's internal actors: a write the journal failed or rejected, a replay it
/// could not finish. Newtonsoft.Json cannot copy them ("Unable to cast object of type
/// 'Newtonsoft.Json.Linq.JObject' to type 'Newtonsoft.Json.Linq.JValue'"), so Akka.NET drops
/// the message and the persistent actor waits in its stash for an answer that never comes.
/// Those messages never leave the process in production either, so the copy keeps a reference
/// to the original exception instead of a serialized one.
/// </summary>
internal sealed class LocalExceptionJsonConverter : JsonConverter
{
    private static readonly ConcurrentDictionary<Guid, Exception> InFlight = new();

    public override bool CanConvert(Type objectType) =>
        typeof(Exception).IsAssignableFrom(objectType);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var id = Guid.NewGuid();
        InFlight[id] = (Exception)value!;
        new JObject
        {
            ["$type"] = $"{value!.GetType().FullName}, {value.GetType().Assembly.GetName().Name}",
            ["exception"] = id,
        }.WriteTo(writer);
    }

    public override object? ReadJson(
        JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var id = JObject.Load(reader)["exception"]!.ToObject<Guid>();
        return InFlight.TryRemove(id, out var exception)
            ? exception
            : throw new JsonSerializationException($"No exception {id} is in flight.");
    }
}
