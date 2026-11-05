using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ohmline.DeviceGateway.Ocpp.Messages;

/// <summary>How payloads are read and written: camelCase, and the vendors' times as UTC.</summary>
public static class OcppJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static JsonElement Element<T>(T payload) =>
        JsonSerializer.SerializeToElement(payload, Options);

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new UnmarkedTimeIsUtc());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    /// <summary>
    /// A vendor quirk: some chargers send local-looking times with no offset at all
    /// (<c>2026-11-04T14:40:00</c>). OCPP's times are UTC, so a time without an offset is read
    /// as UTC, never as the gateway machine's local time.
    /// </summary>
    private sealed class UnmarkedTimeIsUtc : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(
                reader.GetString() ?? throw new JsonException("A time is missing."),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        public override void Write(
            Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime().ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
    }
}
