namespace Ohmline.Charging.Client.Contracts.Requests;

/// <summary><c>POST /app/v1/sessions/{id}/stop</c>: stop the session the route names.</summary>
public sealed record StopChargingRequest(Guid CommandId);
