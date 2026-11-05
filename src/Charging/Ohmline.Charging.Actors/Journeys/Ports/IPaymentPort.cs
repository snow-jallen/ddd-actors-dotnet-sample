using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// How the charging journey reaches whoever takes payments: a port the journey owns, in the
/// journey's words. An adapter behind it translates to the payment side's language. The answer
/// is piped back to the journey's actor as a message, never awaited in its mailbox.
/// </summary>
/// <remarks>
/// Every call carries an idempotency key (section 15.6), and the payment side must honour it: a
/// call that comes again under the same key gets the first call's answer and moves no money.
/// </remarks>
public interface IPaymentPort
{
    /// <summary>Hold this much on the card of the driver's account.</summary>
    Task<PaymentPortResult> PlaceHoldAsync(
        IdempotencyKey key, DriverRef driver, Money amount, CancellationToken cancellationToken);

    /// <summary>Take this much from the hold.</summary>
    Task<PaymentPortResult> CaptureAsync(
        IdempotencyKey key, string holdId, Money amount, CancellationToken cancellationToken);

    /// <summary>
    /// Release the hold placed under <paramref name="hold"/>, taking nothing. There may be
    /// none, if the hold was never placed; then there is nothing to do.
    /// </summary>
    Task<PaymentPortResult> VoidAsync(IdempotencyKey hold, CancellationToken cancellationToken);
}
