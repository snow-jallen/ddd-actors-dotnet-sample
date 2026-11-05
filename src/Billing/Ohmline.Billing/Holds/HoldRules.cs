using Microsoft.AspNetCore.Http;
using Ohmline.Billing.Contracts.Holds;

namespace Ohmline.Billing.Holds;

/// <summary>
/// The canvas's rules for holds (section 5.1), as plain functions over a row: no capture
/// without a hold, never more than the hold, once; a voided hold takes nothing.
/// </summary>
public static class HoldRules
{
    public static HoldDecision Capture(Hold hold, decimal amount, string currency) => hold switch
    {
        { Status: HoldStatus.Voided } => Refused(
            StatusCodes.Status409Conflict,
            HoldProblemCodes.HoldVoided,
            $"Hold {hold.HoldId} was voided."),
        { Status: HoldStatus.Captured } when hold.Captured == amount && hold.Currency == currency
            => new AlreadyDone(hold),
        { Status: HoldStatus.Captured } => Refused(
            StatusCodes.Status409Conflict,
            HoldProblemCodes.AlreadyCaptured,
            $"Hold {hold.HoldId} was captured for {hold.Captured} {hold.Currency}."),
        _ when currency != hold.Currency || amount < 0m || amount > hold.Amount => Refused(
            StatusCodes.Status409Conflict,
            HoldProblemCodes.ExceedsHold,
            $"{amount} {currency} is not within the {hold.Amount} {hold.Currency} held."),
        _ => new Proceed(),
    };

    public static HoldDecision Void(Hold hold) => hold switch
    {
        { Status: HoldStatus.Voided } => new AlreadyDone(hold),
        { Status: HoldStatus.Captured } => Refused(
            StatusCodes.Status409Conflict,
            HoldProblemCodes.AlreadyCaptured,
            $"Hold {hold.HoldId} was captured; there is nothing left to void."),
        _ => new Proceed(),
    };

    private static Refuse Refused(int status, string code, string message) =>
        new(status, code, message);
}
