using System.Text.Json;
using System.Text.Json.Serialization;
using Ohmline.SharedKernel;

namespace Ohmline.ActorShell.Serialization;

/// <summary>
/// Writes a value object as the one raw value it wraps (a charge point id as
/// <c>"OHM-SLC-0042"</c>, a power as <c>22</c>) and reads it back through the value object's own
/// validation, so an invalid value never gets in. It recognises any type that implements
/// <see cref="IValueObject{TSelf, TValue}"/>, which keeps the shared kernel free of serialization
/// attributes.
/// </summary>
public sealed class ValueObjectConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => RawTypeOf(typeToConvert) is not null;

    public override JsonConverter CreateConverter(
        Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(ValueObjectConverter<,>)
            .MakeGenericType(typeToConvert, RawTypeOf(typeToConvert)!))!;

    private static Type? RawTypeOf(Type type) =>
        type.GetInterfaces()
            .Where(contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(IValueObject<,>)
                && contract.GenericTypeArguments[0] == type)
            .Select(contract => contract.GenericTypeArguments[1])
            .SingleOrDefault();
}

internal sealed class ValueObjectConverter<TSelf, TValue> : JsonConverter<TSelf>
    where TSelf : IValueObject<TSelf, TValue>
{
    public override TSelf Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = JsonSerializer.Deserialize<TValue>(ref reader, options);
        try
        {
            return TSelf.Create(raw!);
        }
        catch (ArgumentException invalid)
        {
            throw new JsonException(
                $"Not a valid {typeof(TSelf).Name}: {invalid.Message}", invalid);
        }
    }

    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.Value, options);
}
