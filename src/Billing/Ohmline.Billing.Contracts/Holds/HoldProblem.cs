namespace Ohmline.Billing.Contracts.Holds;

/// <summary>
/// What a caller of the hold API reads from a refusal: the parts of RFC 9457 problem details
/// it needs. Other members of the problem are ignored.
/// </summary>
public sealed record HoldProblem(int? Status, string? Code, string? Detail);
