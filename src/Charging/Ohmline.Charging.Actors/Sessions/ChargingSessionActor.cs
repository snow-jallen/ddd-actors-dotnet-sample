using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Contracts.Budgets;

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

    private readonly TimeProvider _clock;

    // WRONG ON PURPOSE (branch wrong/ch12-s02-actorref-as-identity): the session keeps the
    // actor of its site's power budget, as if an actor reference were the site's identity. It
    // points at one incarnation on one node; passivation, a rebalance or a restart ends it, and
    // whatever is told to it afterwards is a dead letter. Keep the SiteId and send to the
    // site-power-budget region instead.
    private IActorRef _siteBudget = ActorRefs.Nobody;

    public ChargingSessionActor(SessionId sessionId, TimeProvider clock)
        : base(TypeName, sessionId.ToString(), clock)
    {
        _clock = clock;
        Command<UseSiteBudget>(use => _siteBudget = use.Budget);
    }

    public static string PersistenceIdOf(SessionId sessionId) => $"{TypeName}-{sessionId}";

    public static Props Props(SessionId sessionId, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new ChargingSessionActor(sessionId, clock));

    protected override object Accepted(long version) => new CommandAccepted(version);

    protected override object Rejected(SessionError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new CommandRejected(code, message);

    protected override object? ToSnapshot(SessionState state) => state.ToSnapshot();

    protected override SessionState FromSnapshot(object snapshot) =>
        ((ChargingSessionSnapshot)snapshot).ToState();

    /// <summary>An ended session gives its power back, to the actor it remembered.</summary>
    protected override void OnEventApplied(SessionEvent @event)
    {
        if (@event is SessionEnded ended)
        {
            _siteBudget.Tell(new ReleaseAllocation(
                CommandId.New(_clock), ended.SiteId, ended.ConnectorId, ended.SessionId));
        }
    }

    /// <summary>Remember this actor as the site's power budget (the mistake).</summary>
    public sealed record UseSiteBudget(IActorRef Budget) : INoSerializationVerificationNeeded;
}
