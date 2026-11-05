using System.Globalization;

namespace Ohmline.Mobile.Core.Charging;

/// <summary>
/// The most the driver lets one charge cost, held on the card before charging starts: from 1
/// to 500, in whole cents, in the currency Ohmline charges in. The server checks the same
/// range; this copy is for feedback while the driver types.
/// </summary>
public readonly record struct SpendCap
{
    public const string Currency = "USD";

    public const decimal Min = 1m;

    public const decimal Max = 500m;

    private SpendCap(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    /// <summary>The driver's text, read as a number as the phone's language writes it.</summary>
    public static ParseResult<SpendCap> TryParse(string? text, IFormatProvider? culture = null)
    {
        if (!decimal.TryParse(
                text, NumberStyles.Number, culture ?? CultureInfo.CurrentCulture, out var amount))
        {
            return new Invalid($"Choose a spend cap from {Min} to {Max} {Currency}.");
        }

        if (amount is < Min or > Max)
        {
            return new Invalid($"A spend cap is from {Min} to {Max} {Currency}, not {amount}.");
        }

        return decimal.Round(amount, 2) == amount
            ? new Parsed<SpendCap>(new SpendCap(amount))
            : new Invalid("A spend cap is in whole cents.");
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {Currency}");
}
