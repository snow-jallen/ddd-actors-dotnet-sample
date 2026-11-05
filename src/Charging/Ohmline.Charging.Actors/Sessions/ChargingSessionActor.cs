using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
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

    public ChargingSessionActor(SessionId sessionId, TimeProvider clock)
        : base(TypeName, sessionId.ToString(), clock)
    {
    }

    public static string PersistenceIdOf(SessionId sessionId) => $"{TypeName}-{sessionId}";

    /// <summary>
    /// For a test that starts the actor itself, with a clock of its choosing. In a node, the
    /// shard region has the container make it (section 12.3).
    /// </summary>
    public static Props Props(SessionId sessionId, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new ChargingSessionActor(sessionId, clock));

    protected override object Accepted(long version) => new CommandAccepted(version);

    protected override object Rejected(SessionError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new CommandRejected(code, message);

    protected override object? ToSnapshot(SessionState state) => state.ToSnapshot();

    protected override SessionState FromSnapshot(object snapshot) =>
        ((ChargingSessionSnapshot)snapshot).ToState();

    /// <summary>
    /// An ended session changes no more: it leaves memory now, instead of after two idle
    /// minutes. A late command starts it again from the journal and is refused as usual.
    /// </summary>
    protected override void OnEventApplied(SessionEvent @event)
    {
        if (@event is SessionEnded)
        {
            // WRONG ON PURPOSE (branch wrong/ch12-s04-context-stop): the session stops itself.
            // The commands waiting in its stash die with it, and the shard, which was not
            // asked, keeps routing to an actor that is gone. Use Passivate() instead.
            Context.Stop(Self);
        }
    }
}
