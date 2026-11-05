namespace Ohmline.Billing.Contracts.Holds;

/// <summary>
/// The stable codes of the hold API's refusals, in the <c>code</c> extension of its RFC 9457
/// problem details. Programs switch on these; the problem's <c>detail</c> is for people.
/// Append only: Charging's payment port maps each one.
/// </summary>
public static class HoldProblemCodes
{
    /// <summary>404: no hold has this id.</summary>
    public const string UnknownHold = "unknown-hold";

    /// <summary>409: the capture is for more than the hold, or in another currency.</summary>
    public const string ExceedsHold = "exceeds-hold";

    /// <summary>409: the hold was voided, so nothing can be captured.</summary>
    public const string HoldVoided = "hold-voided";

    /// <summary>409: the hold was captured already, for another amount.</summary>
    public const string AlreadyCaptured = "already-captured";

    /// <summary>402: the card would not hold the money (section 16.4).</summary>
    public const string Declined = "declined";

    /// <summary>503: the payment provider did not answer; send the same request again.</summary>
    public const string ProviderUnavailable = "provider-unavailable";
}
