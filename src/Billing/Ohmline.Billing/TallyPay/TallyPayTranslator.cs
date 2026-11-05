using System.Globalization;

namespace Ohmline.Billing.TallyPay;

/// <summary>
/// The anticorruption layer's translator (section 16.4): TallyPay's status codes, minor units
/// and <c>yyyyMMdd</c> dates in, <see cref="PaymentOutcome"/> out. Pure, so its whole table is
/// a test. Nothing outside this folder ever sees a <c>STATUS_CD</c>.
/// </summary>
public static class TallyPayTranslator
{
    public static PaymentOutcome Translate(TallyPayReply reply) => reply.StatusCode switch
    {
        "00" when reply.AuthorizationId is { Length: > 0 } reference
            && DateOf(reply.TransactionDate) is { } on => new Authorized(reference, on),
        "00" => new ProviderUnavailable(
            $"TallyPay approved without a reference or a date: {reply}."),
        "05" => Declined("do-not-honour", "The card's issuer refused the payment."),
        "51" => Declined("insufficient-funds", "There is not enough money on the card."),
        "54" => Declined("card-expired", "The card has expired."),
        "12" => Declined("unknown-authorization", "TallyPay knows no open authorization."),
        "13" => Declined("invalid-amount", "TallyPay refused the amount."),
        "91" => new ProviderUnavailable("The card's issuer is not answering (91)."),
        "96" => new ProviderUnavailable("TallyPay reported a malfunction (96)."),
        var other => Declined(
            "declined-by-provider", $"TallyPay refused with a code Billing does not know: {other}"),
    };

    /// <summary>
    /// TallyPay counts in minor units: cents. A price with fractions of a cent is rounded half
    /// away from zero here, at the border, and nowhere inside Billing.
    /// </summary>
    public static long ToMinorUnits(decimal amount) =>
        (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>TallyPay's business date, <c>20261104</c>; null when it is not one.</summary>
    public static DateOnly? DateOf(string? yyyyMMdd) =>
        DateOnly.TryParseExact(
            yyyyMMdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;

    private static Declined Declined(string code, string message) => new(code, message);
}
