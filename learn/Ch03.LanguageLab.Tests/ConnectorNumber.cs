namespace Ch03.LanguageLab.Tests;

/// <summary>A connector's number on its charge point, from 1. Its default, 0, is invalid.</summary>
public readonly record struct ConnectorNumber
{
    public ConnectorNumber(int value) => Value = value;

    public int Value
    {
        get;
        init => field = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Value), value, "Connectors start at 1.");
    }
}
