namespace Ohmline.Charging.Api.Requests;

/// <summary>The charge point's energy register in Wh, and when it was read.</summary>
public sealed record MeterValueRequest(long RegisterWh, DateTimeOffset MeasuredAt);
