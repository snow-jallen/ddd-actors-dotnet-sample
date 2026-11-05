using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Budgets;

/// <summary>
/// One site's power budget in the shell. The fair-share allocator and the budget's invariant
/// stay in <see cref="SitePowerBudget"/>; this class adds a durable name, Site Energy's
/// replies and the answer to a question about the budget.
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

    protected override object Rejected(SitePowerBudgetError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new BudgetCommandRejected(code, message);

    protected override object? ToSnapshot(SitePowerBudgetState state) => state.ToSnapshot();

    protected override SitePowerBudgetState FromSnapshot(object snapshot) =>
        ((SitePowerBudgetSnapshot)snapshot).ToState();
}
