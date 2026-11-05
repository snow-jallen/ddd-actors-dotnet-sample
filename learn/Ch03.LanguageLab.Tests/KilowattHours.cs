namespace Ch03.LanguageLab.Tests;

/// <summary>Energy in kWh: power over time.</summary>
public readonly record struct KilowattHours(decimal Value)
{
    public override string ToString() => $"{Value} kWh";
}
