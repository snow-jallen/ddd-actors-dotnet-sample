namespace Ohmline.Billing.TallyPay;

/// <summary>
/// What a call to the payment provider came to, in Billing's words: done, refused, or nobody
/// knows. The last one is not a refusal: the same call, under the same idempotency key, may be
/// sent again, and the provider does it at most once.
/// </summary>
public union PaymentOutcome(Authorized, Declined, ProviderUnavailable);

/// <summary>The provider did it, under this reference, on this business day.</summary>
public sealed record Authorized(string Reference, DateOnly On);

/// <summary>The provider refused: a stable code of Billing's, and a sentence for people.</summary>
public sealed record Declined(string Code, string Message);

/// <summary>No usable answer: a timeout, an error, or an answer Billing cannot read.</summary>
public sealed record ProviderUnavailable(string Reason);
