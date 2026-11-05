namespace Ohmline.Charging.Domain.Metering;

/// <summary>Why two meter readings do not give the energy delivered between them.</summary>
public union MeterError(RegisterWentBackwards, ReadingOutOfOrder);

/// <summary>The later register is lower: a meter reset, a meter swap or a firmware fault.</summary>
public sealed record RegisterWentBackwards(MeterReading Previous, MeterReading Current);

/// <summary>The reading was taken before the one it should follow.</summary>
public sealed record ReadingOutOfOrder(MeterReading Previous, MeterReading Current);
