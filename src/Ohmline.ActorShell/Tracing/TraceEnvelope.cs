using System.Diagnostics;

namespace Ohmline.ActorShell.Tracing;

/// <summary>
/// A message with the trace it belongs to (section 17.4): the sender's span as a W3C
/// <c>traceparent</c> and <c>tracestate</c>, strings that cross a mailbox, a node and a journal
/// unchanged. The receiving actor opens it, carries on the trace, and handles the message
/// inside as if it had come alone.
/// </summary>
/// <param name="Message">What was sent; never itself an envelope.</param>
/// <param name="TraceParent">The sender's span, such as
/// <c>00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01</c>.</param>
/// <param name="TraceState">Vendor data that travels with the trace, if any.</param>
public sealed record TraceEnvelope(object Message, string TraceParent, string? TraceState)
{
    /// <summary>
    /// The message in an envelope with the current span, or the message itself when there is no
    /// current span: untraced traffic is sent, and costs, as before.
    /// </summary>
    public static object Wrap(object message) =>
        message is not TraceEnvelope
        && Activity.Current is { IdFormat: ActivityIdFormat.W3C } current
            ? new TraceEnvelope(message, current.Id!, current.TraceStateString)
            : message;

    /// <summary>The message inside an envelope, or the message itself.</summary>
    public static object Unwrap(object message) =>
        message is TraceEnvelope envelope ? envelope.Message : message;

    /// <summary>
    /// The sender's span, as the parent or the link of the receiver's (section 17.7); default
    /// when <see cref="TraceParent"/> is not a W3C traceparent.
    /// </summary>
    public ActivityContext ParentContext =>
        ActivityContext.TryParse(TraceParent, TraceState, isRemote: true, out var context)
            ? context
            : default;
}
