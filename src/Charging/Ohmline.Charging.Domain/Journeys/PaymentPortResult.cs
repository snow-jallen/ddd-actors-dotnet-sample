using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// What the journey's payment port answers, in the journey's words: told back to the journey's
/// actor, so a closed record (ADR 0003). Whoever takes payments behind the port (Billing, from
/// Chapter 16) has words of its own, which the port translates.
/// </summary>
public closed record PaymentPortResult;

/// <summary>The money is held on the driver's card, under this reference.</summary>
public sealed record HoldPlaced(string HoldId, Money Amount) : PaymentPortResult;

/// <summary>The card would not hold the money: a stable code, and a sentence for people.</summary>
public sealed record HoldDeclined(string Code, string Message) : PaymentPortResult;

/// <summary>The hold is released; nothing was taken.</summary>
public sealed record HoldVoided(string HoldId) : PaymentPortResult;

/// <summary>This much was taken from the hold.</summary>
public sealed record PaymentCaptured(string HoldId, Money Amount) : PaymentPortResult;

/// <summary>Nothing was taken this time: a stable code, and a sentence for people.</summary>
public sealed record CaptureFailed(string HoldId, string Code, string Message)
    : PaymentPortResult;
