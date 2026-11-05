using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// Payments in this process's memory, until Billing takes them over (section 16.5): every hold
/// is placed, a capture takes at most what is held, and a void releases the hold. Nothing leaves
/// the process and nothing survives it, so a node registers it only in Development.
/// </summary>
public sealed class InMemoryPaymentPort(ILogger<InMemoryPaymentPort> log) : IPaymentPort
{
    private readonly ConcurrentDictionary<string, Money> _holds = new();

    public Task<PaymentPortResult> PlaceHoldAsync(
        DriverRef driver, Money amount, CancellationToken cancellationToken)
    {
        var holdId = $"hold-{Guid.NewGuid():N}";
        _holds[holdId] = amount;
        log.LogInformation("Held {Amount} for {Driver} as {HoldId}", amount, driver, holdId);
        return Answer(new HoldPlaced(holdId, amount));
    }

    public Task<PaymentPortResult> CaptureAsync(
        string holdId, Money amount, CancellationToken cancellationToken)
    {
        if (!_holds.TryGetValue(holdId, out var held))
        {
            return Answer(new CaptureFailed(holdId, "unknown-hold", $"No hold {holdId}."));
        }

        if (amount.Currency != held.Currency || amount > held)
        {
            return Answer(new CaptureFailed(
                holdId, "exceeds-hold", $"{amount} is more than the {held} held."));
        }

        _holds.TryRemove(holdId, out _);
        log.LogInformation("Captured {Amount} from {HoldId}", amount, holdId);
        return Answer(new PaymentCaptured(holdId, amount));
    }

    public Task<PaymentPortResult> VoidAsync(string holdId, CancellationToken cancellationToken)
    {
        _holds.TryRemove(holdId, out _);
        log.LogInformation("Voided {HoldId}", holdId);
        return Answer(new HoldVoided(holdId));
    }

    private static Task<PaymentPortResult> Answer(PaymentPortResult result) =>
        Task.FromResult(result);
}
