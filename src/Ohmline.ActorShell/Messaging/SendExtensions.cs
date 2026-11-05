using System.Runtime.CompilerServices;
using Akka.Actor;

namespace Ohmline.ActorShell.Messaging;

/// <summary>
/// Telling a union the safe way. A union is a struct that holds one of its cases. Told as it is,
/// the struct is boxed and sent: the recipient's <c>Receive&lt;Case&gt;</c> never matches it,
/// and a serializer restores it empty (section 3.9). <c>Send</c> tells the case instead, so a
/// union never goes on the wire. Use it whenever the value in hand is a union.
/// </summary>
public static class SendExtensions
{
    extension(IActorRef recipient)
    {
        /// <summary>Tells the case, from the current actor (no one, outside an actor).</summary>
        public void Send<TProtocol>(TProtocol message)
            where TProtocol : IUnion =>
            recipient.Tell(CaseOf(message));

        /// <summary>Tells the union's case, from <paramref name="sender"/>.</summary>
        public void Send<TProtocol>(TProtocol message, IActorRef sender)
            where TProtocol : IUnion =>
            recipient.Tell(CaseOf(message), sender);
    }

    private static object CaseOf<TProtocol>(TProtocol message)
        where TProtocol : IUnion =>
        message.Value ?? throw new ArgumentException(
            $"This {typeof(TProtocol).Name} is default: it holds no case to send.",
            nameof(message));
}
