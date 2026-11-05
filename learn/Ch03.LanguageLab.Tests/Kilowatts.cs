namespace Ch03.LanguageLab.Tests;

/// <summary>
/// Power in kW. The guard lives in the init accessor, so the constructor and every
/// with-expression go through it.
/// </summary>
public readonly record struct Kilowatts
{
    public Kilowatts(decimal value) => Value = value;

    public decimal Value
    {
        get;
        init => field = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Value), value, "Negative power.");
    }

    public override string ToString() => $"{Value} kW";
}
