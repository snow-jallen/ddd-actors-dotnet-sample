namespace Ohmline.Billing.Contracts.Holds;

/// <summary>
/// <c>POST /holds/{holdId}/capture</c>: take this much from the hold, at most what is held, and
/// release the rest. A hold is captured once; the same capture sent again gets the same answer.
/// </summary>
public sealed record CapturePaymentRequest(decimal Amount, string Currency);
