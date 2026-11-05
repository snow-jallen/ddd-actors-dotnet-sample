using System.Runtime.CompilerServices;

namespace Ohmline.ActorShell.Tracing;

/// <summary>What a span calls a message: a few dozen names per context, never an id.</summary>
public static class MessageNames
{
    /// <summary>
    /// The message's type name, such as <c>ReserveConnector</c>. For a union, which should never
    /// be sent but may be by mistake (section 3.9), the name of the case it holds rather than
    /// the union's own.
    /// </summary>
    public static string Of(object message) => message switch
    {
        IUnion { Value: { } value } => value.GetType().Name,
        _ => message.GetType().Name,
    };
}
