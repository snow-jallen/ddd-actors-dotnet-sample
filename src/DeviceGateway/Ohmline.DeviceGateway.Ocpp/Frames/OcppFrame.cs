using System.Text.Json;

namespace Ohmline.DeviceGateway.Ocpp.Frames;

/// <summary>
/// The three kinds of message on an OCPP-J WebSocket, in the style of OCPP 1.6J (section 16.6):
/// a call, its result, or its error. Every one is a JSON array whose first element says which.
/// A union, because a frame is parsed, matched and answered, never persisted or told.
/// </summary>
/// <remarks>
/// Ohmline's gateway speaks a simplified subset, in the style of OCPP 1.6J. It makes no claim of
/// conformance to any OCPP version.
/// </remarks>
public union OcppFrame(Call, CallResult, CallError)
{
    public string MessageId => this switch
    {
        Call call => call.MessageId,
        CallResult result => result.MessageId,
        CallError error => error.MessageId,
        null => throw new InvalidOperationException("The frame is default: no case."),
    };
}

/// <summary><c>[2, "id", "Action", {payload}]</c>: please do this, or take note of this.</summary>
public sealed record Call(string MessageId, string Action, JsonElement Payload);

/// <summary><c>[3, "id", {payload}]</c>: the answer to the call with the same id.</summary>
public sealed record CallResult(string MessageId, JsonElement Payload);

/// <summary>
/// <c>[4, "id", "ErrorCode", "description", {details}]</c>: the call with the same id could not
/// be handled. The codes are OCPP's, such as <c>FormationViolation</c> or <c>NotImplemented</c>.
/// </summary>
public sealed record CallError(string MessageId, string ErrorCode, string Description);
