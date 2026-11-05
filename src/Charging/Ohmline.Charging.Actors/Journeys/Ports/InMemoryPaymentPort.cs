using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// Payments in this process's memory: every hold is placed, a capture takes at most what is
/// held, and a void releases the hold. Nothing leaves the process and nothing survives it.
/// Retired from the node when Billing took the payments over (section 16.5); tests that run a
/// journey without Billing put it behind the port instead.
/// </summary>
/// <remarks>
/// Every call is idempotent by its key (section 15.6): a hold or a capture asked for again
/// under the same key gets the first answer, and no second hold or capture is made.
/// </remarks>
public sealed class InMemoryPaymentPort(ILogger<InMemoryPaymentPort> log) : IPaymentPort
{
    private readonly ConcurrentDictionary<IdempotencyKey, HoldPlaced> _holds = new();
    private readonly ConcurrentDictionary<IdempotencyKey, PaymentPortResult> _captures = new();
    private readonly ConcurrentDictionary<string, Money> _held = new();

    public Task<PaymentPortResult> PlaceHoldAsync(
        IdempotencyKey key, DriverRef driver, Money amount, CancellationToken cancellationToken)
    {
        var placed = _holds.GetOrAdd(key, _ =>
        {
            var hold = new HoldPlaced($"hold-{Guid.NewGuid():N}", amount);
            _held[hold.HoldId] = amount;
            log.HoldPlaced(amount, driver, hold.HoldId, key);
            return hold;
        });
        return Answer(placed);
    }

    public Task<PaymentPortResult> CaptureAsync(
        IdempotencyKey key, string holdId, Money amount, CancellationToken cancellationToken) =>
        Answer(_captures.GetOrAdd(key, _ => Capture(holdId, amount)));

    public Task<PaymentPortResult> VoidAsync(
        IdempotencyKey hold, CancellationToken cancellationToken)
    {
        if (_holds.TryGetValue(hold, out var placed) && _held.TryRemove(placed.HoldId, out _))
        {
            log.HoldVoided(placed.HoldId, hold);
        }

        return Answer(new HoldVoided(placed?.HoldId ?? hold.Value));
    }

    private PaymentPortResult Capture(string holdId, Money amount)
    {
        if (!_held.TryGetValue(holdId, out var held))
        {
            return new CaptureFailed(holdId, "unknown-hold", $"No hold {holdId}.");
        }

        if (amount.Currency != held.Currency || amount > held)
        {
            return new CaptureFailed(
                holdId, "exceeds-hold", $"{amount} is more than the {held} held.");
        }

        _held.TryRemove(holdId, out _);
        log.Captured(amount, holdId);
        return new PaymentCaptured(holdId, amount);
    }

    private static Task<PaymentPortResult> Answer(PaymentPortResult result) =>
        Task.FromResult(result);
}
