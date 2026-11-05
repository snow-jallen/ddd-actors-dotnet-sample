namespace Ohmline.SiteEnergy.Contracts;

/// <summary>
/// What a site's power budget answers to a command: accepted or rejected. Site Energy's own
/// published vocabulary, a closed record because it crosses the wire (ADR 0003). It has the
/// same shape as Charging's <c>CommandReply</c>, but each context owns its language.
/// </summary>
public closed record BudgetCommandReply;

/// <summary>Every event the command caused is stored; the version is the budget's now.</summary>
public sealed record BudgetCommandAccepted(long Version) : BudgetCommandReply;

/// <summary>Refused and nothing recorded: a stable code, and a sentence for people.</summary>
public sealed record BudgetCommandRejected(string Code, string Message) : BudgetCommandReply;
