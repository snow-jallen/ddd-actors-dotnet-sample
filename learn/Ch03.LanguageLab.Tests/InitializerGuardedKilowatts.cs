namespace Ch03.LanguageLab.Tests;

/// <summary>
/// The guard most people write first: in the property initializer. It runs in the constructor
/// only; a with-expression assigns through the plain init accessor and skips it.
/// </summary>
public readonly record struct InitializerGuardedKilowatts(decimal Value)
{
    public decimal Value { get; init; } = Value >= 0
        ? Value
        : throw new ArgumentOutOfRangeException(nameof(Value), Value, "Power cannot be negative.");
}
