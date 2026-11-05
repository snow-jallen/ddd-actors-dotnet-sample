namespace Ohmline.Billing.Holds;

/// <summary>
/// What a request does to a hold, decided from the hold's row alone: go ahead, answer with what
/// was done before (the same request sent again), or refuse with one of
/// <c>HoldProblemCodes</c> and an HTTP status.
/// </summary>
public union HoldDecision(Proceed, AlreadyDone, Refuse);

public sealed record Proceed;

public sealed record AlreadyDone(Hold Hold);

public sealed record Refuse(int Status, string Code, string Message);
