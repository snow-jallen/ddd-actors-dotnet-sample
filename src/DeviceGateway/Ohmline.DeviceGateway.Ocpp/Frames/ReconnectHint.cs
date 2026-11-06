using System.Globalization;
using System.Net.WebSockets;

namespace Ohmline.DeviceGateway.Ocpp.Frames;

/// <summary>
/// How a gateway that is stopping tells a charger to come back, and when (section 21.7). OCPP
/// 1.6J has no message for it, so this is Ohmline's convention on the WebSocket's close frame:
/// the status 1012, Service Restart in the IANA registry of close codes, and the reason
/// <c>retry-after=N</c>, in whole seconds. A charger that knows the convention waits that long
/// before it reconnects; one that does not still sees a clean close, never a broken connection.
/// </summary>
public static class ReconnectHint
{
    /// <summary>RFC 6455's registry: the service is restarting; reconnect after a while.</summary>
    public const WebSocketCloseStatus ServiceRestart = (WebSocketCloseStatus)1012;

    private const string Prefix = "retry-after=";

    /// <summary>The close frame's reason for a charger told to come back after this long.</summary>
    public static string Describe(TimeSpan retryAfter)
    {
        var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
        return Prefix + seconds.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The wait a close frame asks for, if it is a restart with a hint.</summary>
    public static TimeSpan? Of(WebSocketCloseStatus? status, string? description) =>
        status == ServiceRestart
        && description is not null
        && description.StartsWith(Prefix, StringComparison.Ordinal)
        && int.TryParse(
            description.AsSpan(Prefix.Length),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
}
