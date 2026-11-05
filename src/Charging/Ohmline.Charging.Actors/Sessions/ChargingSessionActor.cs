using System.Collections.Immutable;
using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Sessions;

/// <summary>
/// One charging session's decider in the shell: a durable name and Charging's replies, and
/// nothing else. Every rule is in <see cref="ChargingSession"/>.
/// </summary>
public sealed class ChargingSessionActor
    : DeciderActor<ChargingSession, SessionState, SessionCommand, SessionEvent, SessionError>
{
    /// <summary>Durable vocabulary: the shard type name and the persistence id prefix.</summary>
    public const string TypeName = "charging-session";

    private readonly ChargingMetrics _metrics;

    public ChargingSessionActor(SessionId sessionId, TimeProvider clock, ChargingMetrics metrics)
        : base(TypeName, sessionId.ToString(), clock) =>
        _metrics = metrics;

    public static string PersistenceIdOf(SessionId sessionId) => $"{TypeName}-{sessionId}";

    /// <summary>
    /// For a test that starts the actor itself, with a clock of its choosing, and instruments
    /// of its choosing or none a host collects. In a node, the shard region has the container
    /// make it (section 12.3).
    /// </summary>
    public static Props Props(
        SessionId sessionId, TimeProvider clock, ChargingMetrics? metrics = null)
    {
        var instruments = metrics ?? ChargingMetrics.Unhosted;
        return Akka.Actor.Props.Create(() =>
            new ChargingSessionActor(sessionId, clock, instruments));
    }

    protected override object Accepted(long version) => new CommandAccepted(version);

    protected override object Rejected(SessionError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new CommandRejected(code, message);

    protected override CommandId CommandIdOf(SessionCommand command) => command.CommandId;

    protected override CommandId CausationOf(SessionEvent @event) => @event.CommandId;

    protected override object? ToSnapshot(
        SessionState state, ImmutableArray<RecentCommand> recent) =>
        state.ToSnapshot() is { } snapshot ? snapshot with { RecentCommands = recent } : null;

    protected override (SessionState State, ImmutableArray<RecentCommand> Recent) FromSnapshot(
        object snapshot)
    {
        var stored = (ChargingSessionSnapshot)snapshot;
        return (stored.ToState(), stored.RecentCommands);
    }

    protected override void OnDecided(
        SessionCommand command, Result<ImmutableArray<SessionEvent>, SessionError> decision) =>
        _metrics.Decided(TypeName, PersistenceId, command, decision);

    /// <summary>
    /// Counted as it is stored, never as it is replayed: a session that recovers did not start
    /// again. An ended session changes no more: it leaves memory now, instead of after two idle
    /// minutes. A late command starts it again from the journal and is refused as usual.
    /// </summary>
    protected override void OnEventApplied(SessionEvent @event)
    {
        switch (@event)
        {
            case SessionStarted:
                _metrics.SessionStarted();
                break;
            case SessionEnded ended:
                _metrics.SessionEnded(ended.EnergyDelivered);
                Passivate();
                break;
        }
    }
}
