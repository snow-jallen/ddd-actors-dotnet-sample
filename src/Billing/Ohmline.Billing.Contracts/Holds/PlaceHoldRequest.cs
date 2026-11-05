namespace Ohmline.Billing.Contracts.Holds;

/// <summary>
/// <c>POST /holds</c>: hold this much on the card behind the driver's account before charging.
/// The caller names the hold, so that sending the request again names the same hold and holds
/// no more money; Charging names it by its idempotency key (<c>{journeyId}-hold</c>).
/// </summary>
public sealed record PlaceHoldRequest(
    string HoldId, string DriverRef, decimal Amount, string Currency);
