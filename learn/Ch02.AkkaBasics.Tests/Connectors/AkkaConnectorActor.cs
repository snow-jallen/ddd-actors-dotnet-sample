using Akka.Actor;

namespace Ch02.AkkaBasics.Tests.Connectors;

/// <summary>
/// The connector actor from section 2.2 on Akka.NET. Akka supplies the mailbox, the loop that
/// runs one message at a time, the actor's address (IActorRef) and the reply address (Sender).
/// </summary>
public sealed class AkkaConnectorActor : ReceiveActor
{
    private string? _activeSession;

    public AkkaConnectorActor(string connectorId)
    {
        Receive<StartSession>(start =>
        {
            if (_activeSession is not null)
            {
                Sender.Tell(new SessionRejected(connectorId, start.SessionId));
                return;
            }

            _activeSession = start.SessionId;
            Sender.Tell(new SessionStarted(connectorId, start.SessionId));
        });

        // Handlers are tried in order and the first match wins. An EndSession for any other
        // session matches nothing: Akka.NET treats it as unhandled and sends no reply.
        Receive<EndSession>(end => end.SessionId == _activeSession, end =>
        {
            _activeSession = null;
            Sender.Tell(new SessionEnded(connectorId, end.SessionId));
        });

        Receive<GetStatus>(_ => Sender.Tell(new ConnectorStatus(connectorId, _activeSession)));
    }

    /// <summary>
    /// The recipe Akka.NET uses to create (and re-create) the actor. Never call the constructor
    /// yourself: Akka.Analyzers rejects that with AK1000.
    /// </summary>
    public static Props Props(string connectorId) =>
        Akka.Actor.Props.Create(() => new AkkaConnectorActor(connectorId));
}
