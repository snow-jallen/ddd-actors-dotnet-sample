using System.Collections.Immutable;

namespace Ohmline.DeviceGateway.Ocpp.Messages;

// What a charger sends, and what it is answered, as OCPP 1.6J names it (camelCase on the wire).
// These words stay in the gateway: the translator turns them into Charging's commands.

/// <summary>The first call after a charger connects: who it is.</summary>
public sealed record BootNotificationRequest(
    string ChargePointVendor, string ChargePointModel, string? FirmwareVersion = null);

/// <summary>Accepted, the time, and how often to send a heartbeat, in seconds.</summary>
public sealed record BootNotificationResponse(
    string Status, DateTimeOffset CurrentTime, int Interval);

public sealed record HeartbeatRequest;

public sealed record HeartbeatResponse(DateTimeOffset CurrentTime);

/// <summary>
/// A connector's status, such as <c>Available</c>, <c>Preparing</c>, <c>Charging</c>,
/// <c>SuspendedEV</c>, <c>SuspendedEVSE</c>, <c>Finishing</c> or <c>Faulted</c>; connector 0 is
/// the whole charge point. The error code is the charger's own (<c>GroundFailure</c>).
/// </summary>
public sealed record StatusNotificationRequest(
    int ConnectorId, string ErrorCode, string Status, DateTimeOffset? Timestamp = null);

public sealed record StatusNotificationResponse;

/// <summary>A token presented at the charger: may it charge?</summary>
public sealed record AuthorizeRequest(string IdTag);

public sealed record AuthorizeResponse(IdTagInfo IdTagInfo);

/// <summary>
/// <c>Accepted</c>, <c>Blocked</c>, <c>Expired</c>, <c>Invalid</c> or <c>ConcurrentTx</c>.
/// </summary>
public sealed record IdTagInfo(string Status)
{
    public static IdTagInfo Accepted { get; } = new("Accepted");

    public static IdTagInfo Invalid { get; } = new("Invalid");
}

/// <summary>
/// The car draws power: an OCPP "transaction" has started, which Ohmline calls a charging
/// session. The register is in Wh.
/// </summary>
public sealed record StartTransactionRequest(
    int ConnectorId,
    string IdTag,
    long MeterStart,
    DateTimeOffset Timestamp,
    int? ReservationId = null);

/// <summary>The number the charger names the OCPP transaction by from now on.</summary>
public sealed record StartTransactionResponse(int TransactionId, IdTagInfo IdTagInfo);

/// <summary>Readings taken during an OCPP transaction, each with its sampled values.</summary>
public sealed record MeterValuesRequest(
    int ConnectorId, int? TransactionId, ImmutableArray<MeterValue> MeterValue);

public sealed record MeterValue(
    DateTimeOffset Timestamp, ImmutableArray<SampledValue> SampledValue);

/// <summary>
/// One sampled value, as text. Without a measurand it is the energy register
/// (<c>Energy.Active.Import.Register</c>); without a unit it is in Wh. Vendors differ.
/// </summary>
public sealed record SampledValue(string Value, string? Measurand = null, string? Unit = null);

public sealed record MeterValuesResponse;

/// <summary>The OCPP transaction is over: the last register reading and why it stopped.</summary>
public sealed record StopTransactionRequest(
    int TransactionId,
    long MeterStop,
    DateTimeOffset Timestamp,
    string? IdTag = null,
    string? Reason = null);

public sealed record StopTransactionResponse(IdTagInfo? IdTagInfo);
