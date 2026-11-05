using Akka.Actor;
using Ch03.ClosedLib;

namespace Ch03.LanguageLab.Tests;

/// <summary>Replies with the name of the handler that received the message.</summary>
public sealed class HandlerReportingActor : ReceiveActor
{
    public HandlerReportingActor()
    {
        Receive<Occupied>(occupied => Sender.Tell($"Receive<Occupied>: {occupied.SessionId}"));
        Receive<ConnectorStatus>(
            status => Sender.Tell($"Receive<ConnectorStatus>: {Describe(status)}"));
        Receive<ChargerCommand>(
            command => Sender.Tell($"Receive<ChargerCommand>: {command.GetType().Name}"));
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new HandlerReportingActor());

    private static string Describe(ConnectorStatus status) => status switch
    {
        Available => "available",
        Occupied occupied => $"occupied by {occupied.SessionId}",
        Faulted faulted => $"faulted: {faulted.ErrorCode}",
        null => "empty",
    };
}
