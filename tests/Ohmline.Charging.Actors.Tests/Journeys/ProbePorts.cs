using Akka.Actor;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// The ports, played by test probes: each call is told to the probe, and what the probe's
/// autopilot answers is the port's answer. A probe with an autopilot is a fake context: it
/// answers like Billing or a charge point would, and remembers what it was asked.
/// </summary>
internal sealed class ProbePaymentPort(IActorRef payments) : IPaymentPort
{
    public Task<PaymentPortResult> PlaceHoldAsync(
        DriverRef driver, Money amount, CancellationToken cancellationToken) =>
        payments.Ask<PaymentPortResult>(
            new PortCall.PlaceHold(driver, amount), PortCall.Timeout, cancellationToken);

    public Task<PaymentPortResult> CaptureAsync(
        string holdId, Money amount, CancellationToken cancellationToken) =>
        payments.Ask<PaymentPortResult>(
            new PortCall.Capture(holdId, amount), PortCall.Timeout, cancellationToken);

    public Task<PaymentPortResult> VoidAsync(string holdId, CancellationToken cancellationToken) =>
        payments.Ask<PaymentPortResult>(
            new PortCall.Void(holdId), PortCall.Timeout, cancellationToken);
}

/// <inheritdoc cref="ProbePaymentPort"/>
internal sealed class ProbeChargerPort(IActorRef chargers) : IChargerCommandPort
{
    public Task<ChargerCommandResult> RemoteStartAsync(
        ConnectorId connectorId,
        TokenUid tokenUid,
        SessionId sessionId,
        Power limit,
        CancellationToken cancellationToken) =>
        chargers.Ask<ChargerCommandResult>(
            new PortCall.RemoteStart(connectorId, tokenUid, sessionId, limit),
            PortCall.Timeout,
            cancellationToken);

    public Task<ChargerCommandResult> RemoteStopAsync(
        ConnectorId connectorId, SessionId sessionId, CancellationToken cancellationToken) =>
        chargers.Ask<ChargerCommandResult>(
            new PortCall.RemoteStop(connectorId, sessionId), PortCall.Timeout, cancellationToken);

    public Task<ChargerCommandResult> SetChargingProfileAsync(
        ConnectorId connectorId, Power limit, CancellationToken cancellationToken) =>
        chargers.Ask<ChargerCommandResult>(
            new PortCall.ChargingProfile(connectorId, limit), PortCall.Timeout, cancellationToken);
}

/// <summary>
/// A call to a port, as the probe sees it. Test plumbing that never leaves the process, so it
/// is not serialized; the answers are, like every message Charging receives.
/// </summary>
internal abstract record PortCall : INoSerializationVerificationNeeded
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public sealed record PlaceHold(DriverRef Driver, Money Amount) : PortCall;

    public sealed record Capture(string HoldId, Money Amount) : PortCall;

    public sealed record Void(string HoldId) : PortCall;

    public sealed record RemoteStart(
        ConnectorId ConnectorId, TokenUid TokenUid, SessionId SessionId, Power Limit) : PortCall;

    public sealed record RemoteStop(ConnectorId ConnectorId, SessionId SessionId) : PortCall;

    public sealed record ChargingProfile(ConnectorId ConnectorId, Power Limit) : PortCall;
}
