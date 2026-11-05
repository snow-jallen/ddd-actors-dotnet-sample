using System.Text.Json;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.Requests;

namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// A command as the outbox keeps it: its id, its kind, the aggregate it is for, and the request
/// exactly as it will be sent, already in the published language's JSON.
/// </summary>
public sealed record OutboxCommand(Guid CommandId, string Kind, string AggregateKey, string Payload)
{
    public static OutboxCommand StartCharging(StartChargingRequest request) => new(
        request.CommandId,
        OutboxKinds.StartCharging,
        request.JourneyId.ToString(),
        JsonSerializer.Serialize(request, ChargingClientJsonContext.Default.StartChargingRequest));
}
