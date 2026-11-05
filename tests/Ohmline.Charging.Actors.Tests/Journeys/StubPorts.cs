using System.Collections.Concurrent;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// A payment port that writes down what it is asked and answers at once with what the test
/// chose, or never, when the test chose nothing.
/// </summary>
internal sealed class StubPaymentPort(
    PaymentPortResult? hold = null, PaymentPortResult? capture = null) : IPaymentPort
{
    public ConcurrentQueue<string> Asked { get; } = new();

    public Task<PaymentPortResult> PlaceHoldAsync(
        DriverRef driver, Money amount, CancellationToken cancellationToken) =>
        Answer($"hold {amount} for {driver}", hold);

    public Task<PaymentPortResult> CaptureAsync(
        string holdId, Money amount, CancellationToken cancellationToken) =>
        Answer($"capture {amount} from {holdId}", capture);

    public Task<PaymentPortResult> VoidAsync(string holdId, CancellationToken cancellationToken) =>
        Answer($"void {holdId}", new HoldVoided(holdId));

    private Task<PaymentPortResult> Answer(string request, PaymentPortResult? answer)
    {
        Asked.Enqueue(request);
        return answer is null ? new TaskCompletionSource<PaymentPortResult>().Task
            : Task.FromResult(answer);
    }
}

/// <summary>A charger port that writes down what it is asked and never answers.</summary>
internal sealed class StubChargerPort : IChargerCommandPort
{
    public ConcurrentQueue<string> Asked { get; } = new();

    public Task<ChargerCommandResult> RemoteStartAsync(
        ConnectorId connectorId,
        TokenUid tokenUid,
        SessionId sessionId,
        Power limit,
        CancellationToken cancellationToken) =>
        Unanswered($"start {connectorId} for {tokenUid} at {limit}");

    public Task<ChargerCommandResult> RemoteStopAsync(
        ConnectorId connectorId, SessionId sessionId, CancellationToken cancellationToken) =>
        Unanswered($"stop {connectorId}");

    public Task<ChargerCommandResult> SetChargingProfileAsync(
        ConnectorId connectorId, Power limit, CancellationToken cancellationToken) =>
        Unanswered($"profile {connectorId} at {limit}");

    private Task<ChargerCommandResult> Unanswered(string request)
    {
        Asked.Enqueue(request);
        return new TaskCompletionSource<ChargerCommandResult>().Task;
    }
}
