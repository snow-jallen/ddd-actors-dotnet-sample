using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors.Budgets;

/// <summary>
/// One site's power budget in the shell. The fair-share allocator and the budget's invariant
/// stay in <see cref="SitePowerBudget"/>; this class adds a durable name and Site Energy's
/// replies.
/// </summary>
public sealed class SitePowerBudgetActor
    : DeciderActor<SitePowerBudget, SitePowerBudgetState, SitePowerBudgetCommand,
        SitePowerBudgetEvent, SitePowerBudgetError>
{
    /// <summary>Durable vocabulary: the shard type name and the persistence id prefix.</summary>
    public const string TypeName = "site-power-budget";

    public SitePowerBudgetActor(SiteId siteId, TimeProvider clock)
        : base(TypeName, siteId.Value, clock)
    {
    }

    public static string PersistenceIdOf(SiteId siteId) => $"{TypeName}-{siteId}";

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
