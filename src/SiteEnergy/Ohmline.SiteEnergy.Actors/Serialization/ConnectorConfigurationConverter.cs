using System.Text.Json;
using System.Text.Json.Serialization;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Actors.Serialization;

/// <summary>
/// A connector's configuration, like a site's: written as its values, read back through
/// <c>ConnectorConfiguration.Create</c>.
/// </summary>
internal sealed class ConnectorConfigurationConverter : JsonConverter<ConnectorConfiguration>
{
    public override ConnectorConfiguration Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var shape = JsonSerializer.Deserialize<Shape>(ref reader, options)
            ?? throw new JsonException("A connector configuration cannot be null.");
        return SiteConfigurationConverter.Checked(() => ConnectorConfiguration.Create(
            shape.ConnectorId, shape.MinPower, shape.MaxPower, shape.Priority));
    }

    public override void Write(
        Utf8JsonWriter writer, ConnectorConfiguration value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(
            writer,
            new Shape(value.ConnectorId, value.MinPower, value.MaxPower, value.Priority),
            options);

    private sealed record Shape(
        ConnectorId ConnectorId, Power MinPower, Power MaxPower, PriorityClass Priority);
}
