namespace Ohmline.DeviceGateway.Ocpp.Frames;

/// <summary>
/// A text that is not a frame. With a message id, the charger can be answered with a
/// <see cref="CallError"/>; without one, there is nobody to answer.
/// </summary>
public sealed record FrameError(string? MessageId, string ErrorCode, string Description)
{
    public CallError? Reply =>
        MessageId is { } id ? new CallError(id, ErrorCode, Description) : null;
}
