namespace Ohmline.ActorShell.Tests.Serialization;

/// <summary>A closed hierarchy held inside a message, as a tariff holds its components.</summary>
public closed record Fault;

public sealed record GroundFailure(int LeakageMilliamps) : Fault;

public sealed record OverTemperature(decimal Celsius) : Fault;

public sealed record FaultReport(string ChargePoint, Fault Fault);
