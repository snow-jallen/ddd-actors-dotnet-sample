using System.Collections.Immutable;
using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Budgets;

/// <summary>
/// One site's power budget in the shell. The fair-share allocator and the budget's invariant
/// stay in <see cref="SitePowerBudget"/>; this class adds a durable name, Site Energy's
/// replies and the answer to a question about the budget. A request for power is answered
/// with what the connector may draw now, read from the state the request left behind.
/// </summary>
public sealed class SitePowerBudgetActor
    : DeciderActor<SitePowerBudget, SitePowerBudgetState, SitePowerBudgetCommand,
        SitePowerBudgetEvent, SitePowerBudgetError>
{
    /// <summary>Durable vocabulary: the shard type name and the persistence id prefix.</summary>
    public const string TypeName = "site-power-budget";

    public SitePowerBudgetActor(SiteId siteId, TimeProvider clock)
        : base(TypeName, siteId.Value, clock) =>
        Command<GetSitePowerBudget>(_ => Sender.Tell(State.ToView(siteId, LastSequenceNr)));

    public static string PersistenceIdOf(SiteId siteId) => $"{TypeName}-{siteId}";

    /// <summary>
    /// For a test that starts the actor itself, with a clock of its choosing. In a node, the
    /// shard region has the container make it (section 12.3).
    /// </summary>
    public static Props Props(SiteId siteId, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new SitePowerBudgetActor(siteId, clock));

    protected override object Accepted(long version) => new BudgetCommandAccepted(version);

    /// <summary>
    /// A request is answered with the connector's allocation, or with its place in the queue.
    /// The answer comes from the state, not from the events, so a request sent again, which
    /// changes nothing, gets the allocation as it is now (section 15.6).
    /// </summary>
    protected override object Accepted(SitePowerBudgetCommand command, long version) =>
        (State, command) switch
        {
            (Configured site, RequestAllocation request)
                when site.RequestFor(request.ConnectorId)?.SessionId == request.SessionId =>
                site.Allocations
                        .Where(allocation => allocation.ConnectorId == request.ConnectorId)
                        .Select(allocation => (Power?)allocation.Power)
                        .FirstOrDefault() is { } allocated
                    ? new PowerAllocated(
                        request.SiteId,
                        request.ConnectorId,
                        request.SessionId,
                        allocated,
                        version)
                    : new QueuedForPower(
                        request.SiteId, request.ConnectorId, request.SessionId, version),
            _ => Accepted(version),
        };

    protected override object Rejected(SitePowerBudgetError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new BudgetCommandRejected(code, message);

    protected override CommandId CommandIdOf(SitePowerBudgetCommand command) => command.CommandId;

    protected override CommandId CausationOf(SitePowerBudgetEvent @event) => @event.CommandId;

    protected override object? ToSnapshot(
        SitePowerBudgetState state, ImmutableArray<RecentCommand> recent) =>
        state.ToSnapshot() is { } snapshot ? snapshot with { RecentCommands = recent } : null;

    protected override (SitePowerBudgetState State, ImmutableArray<RecentCommand> Recent)
        FromSnapshot(object snapshot)
    {
        var stored = (SitePowerBudgetSnapshot)snapshot;
        return (stored.ToState(), stored.RecentCommands);
    }
}
