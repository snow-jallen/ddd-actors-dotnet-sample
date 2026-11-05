namespace Ohmline.Billing.Contracts.Holds;

/// <summary>
/// A hold as Billing knows it now: its <see cref="HoldStatus"/>, what was held and, once
/// captured, what was taken. Every successful answer of the hold API is one of these.
/// </summary>
public sealed record HoldResponse(
    string HoldId, string Status, decimal Amount, string Currency, decimal? Captured);
