using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Actors.Serialization;

/// <summary>
/// A site configuration has only a private constructor, so that nothing builds an unchecked one.
/// This converter writes its values and reads them back through <c>SiteConfiguration.Create</c>,
/// which checks them again. The domain needs no serialization attribute.
/// </summary>
internal sealed class SiteConfigurationConverter : JsonConverter<SiteConfiguration>
{
    public override SiteConfiguration Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var shape = JsonSerializer.Deserialize<Shape>(ref reader, options)
            ?? throw new JsonException("A site configuration cannot be null.");
        return Checked(() => SiteConfiguration.Create(
            shape.SiteId, shape.GridLimit, shape.SafetyMargin, shape.TimeZoneId, shape.Connectors));
    }

    public override void Write(
        Utf8JsonWriter writer, SiteConfiguration value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(
            writer,
            new Shape(
                value.SiteId,
                value.GridLimit,
                value.SafetyMargin,
                value.TimeZoneId,
                value.Connectors),
            options);

    internal static T Checked<T>(Func<T> create)
    {
        try
        {
            return create();
        }
        catch (ArgumentException invalid)
        {
            throw new JsonException($"Not a valid {typeof(T).Name}: {invalid.Message}", invalid);
        }
    }

    private sealed record Shape(
        SiteId SiteId,
        Power GridLimit,
        Power SafetyMargin,
        string TimeZoneId,
        ImmutableArray<ConnectorConfiguration> Connectors);
}
