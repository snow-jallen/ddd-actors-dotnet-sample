namespace Ohmline.SharedKernel.Monetary;

/// <summary>
/// How amounts are rounded: to how many decimals, and which way. An implicit concept made
/// explicit: a tariff names its policy for its currency, and nothing else in Ohmline calls
/// <c>Math.Round</c> on money. Calculations stay unrounded until a policy rounds the result.
/// </summary>
public readonly record struct RoundingPolicy
{
    public RoundingPolicy(int decimals, MidpointRounding mode)
    {
        Decimals = decimals;
        Mode = mode;
    }

    public int Decimals
    {
        get;
        init => field = value is >= 0 and <= 28
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Decimals), value, "Use 0 to 28.");
    }

    public MidpointRounding Mode
    {
        get;
        init => field = Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Mode), value, "Unknown rounding.");
    }

    /// <summary>Whole cents, halves away from zero, as on a receipt in dollars or euros.</summary>
    public static RoundingPolicy Cents { get; } = new(2, MidpointRounding.AwayFromZero);

    public static Result<RoundingPolicy, ValidationError> TryCreate(
        int decimals, MidpointRounding mode) =>
        (decimals, mode) switch
        {
            (< 0 or > 28, _) => Result.Fail<ValidationError>(
                new OutOfRange("Rounding decimals", decimals, "0 to 28")),
            (_, var unknown) when !Enum.IsDefined(unknown) => Result.Fail<ValidationError>(
                new OutOfRange("Rounding mode", (int)unknown, "a MidpointRounding value")),
            _ => Result.Ok(new RoundingPolicy(decimals, mode)),
        };

    public Money Round(Money money) =>
        money with { Amount = decimal.Round(money.Amount, Decimals, Mode) };

    public override string ToString() => $"{Decimals} decimals, {Mode}";
}
