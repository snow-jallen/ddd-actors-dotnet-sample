using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// How the charging journey reaches whoever takes payments: a port the journey owns, in the
/// journey's words. An adapter behind it translates to the payment side's language. The answer
/// is piped back to the journey's actor as a message, never awaited in its mailbox.
/// </summary>
public interface IPaymentPort
{
    /// <summary>Hold this much on the card of the driver's account.</summary>
    Task<PaymentPortResult> PlaceHoldAsync(
        DriverRef driver, Money amount, CancellationToken cancellationToken);

    /// <summary>Take this much from the hold.</summary>
    Task<PaymentPortResult> CaptureAsync(
        string holdId, Money amount, CancellationToken cancellationToken);

    /// <summary>Release the hold, taking nothing.</summary>
    Task<PaymentPortResult> VoidAsync(string holdId, CancellationToken cancellationToken);
}
