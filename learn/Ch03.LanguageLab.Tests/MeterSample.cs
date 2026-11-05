namespace Ch03.LanguageLab.Tests;

/// <summary>A reading of a charger's energy register: it counts Wh and never decreases.</summary>
public sealed record MeterSample(DateTimeOffset TakenAt, long RegisterWh);
