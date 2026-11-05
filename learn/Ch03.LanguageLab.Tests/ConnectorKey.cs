namespace Ch03.LanguageLab.Tests;

/// <summary>A record struct: a value type with the same generated members as a record class.</summary>
public readonly record struct ConnectorKey(string ChargePointId, int Number);
