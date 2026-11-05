using Ohmline.Billing.Contracts.Holds;

namespace Ohmline.Billing.Holds;

/// <summary>A row of <c>billing.holds</c>: the hold as it is now, not how it got there.</summary>
public sealed record Hold(
    string HoldId,
    string DriverRef,
    decimal Amount,
    string Currency,
    string Status,
    decimal? Captured)
{
    public HoldResponse ToResponse() => new(HoldId, Status, Amount, Currency, Captured);
}
