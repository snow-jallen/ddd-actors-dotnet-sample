using Ohmline.Billing.Contracts.Holds;
using Ohmline.Billing.Holds;

namespace Ohmline.Billing.Tests.Holds;

/// <summary>The canvas's rules for holds, one row per case.</summary>
public sealed class HoldRulesTests
{
    private static readonly Hold Placed = new("h-1", "drv-7f3a91c2", 50m, "USD", "placed", null);

    public static TheoryData<string, decimal?, decimal, string, string> Captures => new()
    {
        // status, captured before, capture amount, currency, then
        { "placed", null, 12.60m, "USD", "proceed" },
        { "placed", null, 50m, "USD", "proceed" },
        { "placed", null, 50.01m, "USD", HoldProblemCodes.ExceedsHold },
        { "placed", null, 12.60m, "EUR", HoldProblemCodes.ExceedsHold },
        { "captured", 12.60m, 12.60m, "USD", "already done" },
        { "captured", 12.60m, 13m, "USD", HoldProblemCodes.AlreadyCaptured },
        { "voided", null, 12.60m, "USD", HoldProblemCodes.HoldVoided },
    };

    [Theory]
    [MemberData(nameof(Captures))]
    public void A_capture_takes_at_most_what_is_held_and_only_once(
        string status, decimal? before, decimal amount, string currency, string then)
    {
        var hold = Placed with { Status = status, Captured = before };

        Outcome(HoldRules.Capture(hold, amount, currency)).ShouldBe(then);
    }

    [Theory]
    [InlineData("placed", "proceed")]
    [InlineData("voided", "already done")]
    [InlineData("captured", HoldProblemCodes.AlreadyCaptured)]
    public void A_void_releases_a_hold_that_took_nothing(string status, string then)
    {
        Outcome(HoldRules.Void(Placed with { Status = status })).ShouldBe(then);
    }

    private static string Outcome(HoldDecision decision) => decision switch
    {
        Proceed => "proceed",
        AlreadyDone => "already done",
        Refuse refusal => refusal.Code,
        null => "nothing",
    };
}
