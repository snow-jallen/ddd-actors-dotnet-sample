using System.Text.Json;

namespace Ohmline.Charging.Contracts.Integration;

/// <summary>How the Published Language is written in JSON, by Charging and its readers.</summary>
public static class PublishedLanguage
{
    /// <summary>
    /// camelCase names, as on the web, and the <c>type</c> discriminator accepted anywhere in an
    /// event's object: a PostgreSQL <c>jsonb</c> column stores keys in an order of its own.
    /// </summary>
    public static JsonSerializerOptions Json { get; } = ReadOnly(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
        });

    private static JsonSerializerOptions ReadOnly(JsonSerializerOptions options)
    {
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
