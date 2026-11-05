using System.Diagnostics;
using Ohmline.DeviceGateway.Diagnostics;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DeviceGateway.Connections;

/// <summary>
/// The span each frame from a charger is received in (section 17.7): the root of a trace of
/// its own, because the connection's span lasts as long as the socket, and a link to that span
/// says which connection the frame came on.
/// </summary>
public static class FrameSpans
{
    /// <summary>
    /// Starts the frame's span, current from here on, or returns null when nothing listens to
    /// <c>Ohmline.DeviceGateway</c>.
    /// </summary>
    public static Activity? Start(ChargePointId chargePointId, ActivityContext? connection)
    {
        // A root, whatever is current: the connection is a link, not a parent.
        Activity.Current = null;
        var span = DeviceGatewayDiagnostics.ActivitySource.StartActivity(
            "receive ocpp",
            ActivityKind.Consumer,
            parentContext: default,
            links: connection is { } connected ? [new ActivityLink(connected)] : null);
        span?.SetTag("messaging.system.name", "ocpp");
        span?.SetTag("messaging.operation.type", "receive");
        span?.SetTag("ohmline.charge_point.id", chargePointId.Value);
        return span;
    }
}
