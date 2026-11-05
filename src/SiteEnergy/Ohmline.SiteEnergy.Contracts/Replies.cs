using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Contracts;

/// <summary>
/// What a site's power budget answers to a command: accepted or rejected. Site Energy's own
/// published vocabulary, a closed record because it crosses the wire (ADR 0003). It has the
/// same shape as Charging's <c>CommandReply</c>, but each context owns its language. A request
/// for power gets an answer of its own, which says how much the connector may draw.
/// </summary>
public closed record BudgetCommandReply;

/// <summary>Every event the command caused is stored; the version is the budget's now.</summary>
public sealed record BudgetCommandAccepted(long Version) : BudgetCommandReply;

/// <summary>Refused and nothing recorded: a stable code, and a sentence for people.</summary>
public sealed record BudgetCommandRejected(string Code, string Message) : BudgetCommandReply;

/// <summary>
/// The request is admitted: the connector may draw this much now. A request that is asked again
/// for the same session gets the allocation it has now, so asking twice is safe.
/// </summary>
public sealed record PowerAllocated(
    SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId, Power Allocated, long Version)
    : BudgetCommandReply;

/// <summary>
/// The request is open, but its minimum does not fit now: the connector gets its share when a
/// rebalance makes room for it.
/// </summary>
public sealed record QueuedForPower(
    SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId, long Version)
    : BudgetCommandReply;
