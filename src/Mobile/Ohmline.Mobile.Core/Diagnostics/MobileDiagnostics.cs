using System.Diagnostics;

namespace Ohmline.Mobile.Core.Diagnostics;

/// <summary>
/// The driver app's telemetry port (section 19.8): one <see cref="ActivitySource"/>, named
/// <c>Ohmline.Mobile</c>, for the spans the app starts. BCL types only, like the server's
/// diagnostics classes: the head decides whether anything listens, and with nothing listening
/// a span costs a null check.
/// </summary>
/// <remarks>
/// No user input goes into a span: not the connector the driver typed, not the token. The
/// command's id and kind are the app's own values, and enough to find the trace.
/// </remarks>
public static class MobileDiagnostics
{
    /// <summary>Durable: the head and the server's dashboards find the app's spans by it.</summary>
    public const string Name = "Ohmline.Mobile";

    public const string CommandIdTag = "ohmline.command.id";

    public const string CommandKindTag = "ohmline.command.kind";

    public const string AttemptTag = "ohmline.command.attempt";

    public static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>The driver's tap that gave a command: where its trace begins.</summary>
    public static Activity? StartTap(string kind) =>
        ActivitySource.StartActivity($"tap {kind}")?.SetTag(CommandKindTag, kind);

    /// <summary>
    /// One attempt to send a command, perhaps hours after the tap. A child of the tap while the
    /// app is the same process; after a restart, the tap's span is long gone with the process
    /// that started it, so the attempt starts a trace of its own, linked to the tap.
    /// </summary>
    public static Activity? StartSend(
        string kind, Guid commandId, int attempt, string? tap, bool sameProcess)
    {
        var tapped = ActivityContext.TryParse(tap, null, out var context) ? context : default;
        var span = sameProcess || tapped == default
            ? ActivitySource.StartActivity($"send {kind}", ActivityKind.Internal, tapped)
            : ActivitySource.StartActivity(
                $"send {kind}",
                ActivityKind.Internal,
                parentContext: default,
                links: [new ActivityLink(tapped)]);
        return span?.SetTag(CommandIdTag, commandId.ToString())
            .SetTag(CommandKindTag, kind)
            .SetTag(AttemptTag, attempt);
    }

    /// <summary>
    /// The app applying a push. A push does not carry the server's trace the way a request
    /// does, so the span links to the projection's span the push names, when it names one.
    /// </summary>
    public static Activity? StartApply(string method, string? projection) =>
        ActivityContext.TryParse(projection, null, out var context)
            ? ActivitySource.StartActivity(
                $"apply {method}", ActivityKind.Consumer, default(ActivityContext),
                links: [new ActivityLink(context)])
            : ActivitySource.StartActivity($"apply {method}", ActivityKind.Consumer);
}
