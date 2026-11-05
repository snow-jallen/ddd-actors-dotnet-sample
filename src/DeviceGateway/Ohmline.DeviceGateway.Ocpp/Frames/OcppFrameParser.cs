using System.Buffers;
using System.Text;
using System.Text.Json;
using Ohmline.SharedKernel;

namespace Ohmline.DeviceGateway.Ocpp.Frames;

/// <summary>
/// Reads and writes OCPP-J frames with System.Text.Json, over the JSON array as it is
/// (<c>[2, "id", "Action", {...}]</c>). The payload stays JSON until the translator knows which
/// action it belongs to.
/// </summary>
public static class OcppFrameParser
{
    private const int CallType = 2;
    private const int CallResultType = 3;
    private const int CallErrorType = 4;

    public static Result<OcppFrame, FrameError> Parse(string text)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(text);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Fail(null, "Not JSON.");
        }

        if (root.ValueKind is not JsonValueKind.Array || root.GetArrayLength() < 3)
        {
            return Fail(null, "A frame is a JSON array of at least three elements.");
        }

        if (root[1].ValueKind is not JsonValueKind.String || root[1].GetString() is not { } id)
        {
            return Fail(null, "The second element of a frame is its message id, a string.");
        }

        return (Kind(root[0]), root.GetArrayLength()) switch
        {
            (CallType, 4) when root[2].ValueKind is JsonValueKind.String
                && root[3].ValueKind is JsonValueKind.Object =>
                Result.Ok<OcppFrame>(new Call(id, root[2].GetString()!, root[3])),
            (CallResultType, 3) when root[2].ValueKind is JsonValueKind.Object =>
                Result.Ok<OcppFrame>(new CallResult(id, root[2])),
            (CallErrorType, 5) when root[2].ValueKind is JsonValueKind.String =>
                Result.Ok<OcppFrame>(new CallError(
                    id, root[2].GetString()!, root[3].GetString() ?? "")),
            _ => Fail(id, "The elements do not make a call, a call result or a call error."),
        };
    }

    public static string Write(OcppFrame frame)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartArray();
            switch (frame)
            {
                case Call call:
                    json.WriteNumberValue(CallType);
                    json.WriteStringValue(call.MessageId);
                    json.WriteStringValue(call.Action);
                    call.Payload.WriteTo(json);
                    break;
                case CallResult result:
                    json.WriteNumberValue(CallResultType);
                    json.WriteStringValue(result.MessageId);
                    result.Payload.WriteTo(json);
                    break;
                case CallError error:
                    json.WriteNumberValue(CallErrorType);
                    json.WriteStringValue(error.MessageId);
                    json.WriteStringValue(error.ErrorCode);
                    json.WriteStringValue(error.Description);
                    json.WriteStartObject();
                    json.WriteEndObject();
                    break;
                case null:
                    throw new InvalidOperationException("The frame is default: no case.");
            }

            json.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static int? Kind(JsonElement element) =>
        element.ValueKind is JsonValueKind.Number && element.TryGetInt32(out var kind)
            ? kind
            : null;

    private static Err<FrameError> Fail(string? messageId, string description) =>
        Result.Fail(new FrameError(messageId, "FormationViolation", description));
}
