using Ohmline.Billing.TallyPay;

namespace Ohmline.Billing.Tests.TallyPay;

/// <summary>
/// TallyPay's answers in Billing's words, one row per status code: the whole anticorruption
/// layer's vocabulary on one page.
/// </summary>
public sealed class TallyPayTranslatorTests
{
    public static TheoryData<string?, string?, string?, string> Replies => new()
    {
        // STATUS_CD, AUTH_ID, TXN_DT, then
        { "00", "TP12345", "20261104", "authorized TP12345 on 2026-11-04" },
        { "00", null, "20261104", "unavailable" },
        { "00", "TP12345", "04.11.2026", "unavailable" },
        { "05", null, "20261104", "declined do-not-honour" },
        { "51", null, "20261104", "declined insufficient-funds" },
        { "54", null, "20261104", "declined card-expired" },
        { "12", "TP12345", "20261104", "declined unknown-authorization" },
        { "13", "TP12345", "20261104", "declined invalid-amount" },
        { "91", null, "20261104", "unavailable" },
        { "96", null, "20261104", "unavailable" },
        { "77", null, "20261104", "declined declined-by-provider" },
        { null, null, null, "declined declined-by-provider" },
    };

    [Theory]
    [MemberData(nameof(Replies))]
    public void Each_answer_of_TallyPay_is_an_outcome_of_Billing(
        string? status, string? authorization, string? date, string then)
    {
        var outcome = TallyPayTranslator.Translate(
            new TallyPayReply(status, authorization, date, "WHATEVER IT SAYS"));

        Show(outcome).ShouldBe(then);
    }

    public static TheoryData<decimal, long> Amounts => new()
    {
        { 50m, 5000 },
        { 12.60m, 1260 },
        { 3.087m, 309 },
        { 3.085m, 309 },
        { 3.084m, 308 },
    };

    [Theory]
    [MemberData(nameof(Amounts))]
    public void Amounts_go_to_TallyPay_in_whole_cents(decimal amount, long minorUnits)
    {
        TallyPayTranslator.ToMinorUnits(amount).ShouldBe(minorUnits);
    }

    private static string Show(PaymentOutcome outcome) => outcome switch
    {
        Authorized authorized => $"authorized {authorized.Reference} on {authorized.On:yyyy-MM-dd}",
        Declined declined => $"declined {declined.Code}",
        ProviderUnavailable => "unavailable",
        null => "nothing",
    };
}
