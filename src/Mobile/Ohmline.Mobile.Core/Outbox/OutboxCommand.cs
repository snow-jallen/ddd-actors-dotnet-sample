using System.Text.Json;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Requests;

namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// A command as the outbox keeps it: its id, its kind, the aggregate it is for, and the request
/// exactly as it will be sent, already in the published language's JSON. From section 19.8 it
/// also keeps the traceparent of the tap that gave it, so that a send hours later still traces
/// back to the tap.
/// </summary>
public sealed record OutboxCommand(
    Guid CommandId, string Kind, string AggregateKey, string Payload, string? TraceParent = null)
{
    public static OutboxCommand StartCharging(
        StartChargingRequest request, string? traceParent = null) => new(
        request.CommandId,
        OutboxKinds.StartCharging,
        request.JourneyId.ToString(),
        JsonSerializer.Serialize(request, ChargingClientJsonContext.Default.StartChargingRequest),
        traceParent);

    public static OutboxCommand StopCharging(
        Guid sessionId, StopChargingRequest request, string? traceParent = null) => new(
        request.CommandId,
        OutboxKinds.StopCharging,
        sessionId.ToString(),
        JsonSerializer.Serialize(request, ChargingClientJsonContext.Default.StopChargingRequest),
        traceParent);
}
