namespace Ohmline.Charging.Api.Requests;

/// <summary>Hold the connector for this token, such as <c>04A2B3C4D5E6F7</c>.</summary>
public sealed record ReservationRequest(string TokenUid);
