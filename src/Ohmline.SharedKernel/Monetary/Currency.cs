namespace Ohmline.SharedKernel.Monetary;

/// <summary>
/// A currency, by its ISO 4217 code. The constructor takes the code as ISO writes it (USD);
/// TryCreate also accepts what people type (" usd ") and returns the canonical form.
/// </summary>
public readonly record struct Currency : IValueObject<Currency, string>
{
    public Currency(string code) => Code = code;

    public string Code
    {
        get;
        init => field = IsIsoCode(value)
            ? value
            : throw new ArgumentException($"'{value}' is not an ISO 4217 code.", nameof(Code));
    }

    string IValueObject<Currency, string>.Value => Code;

    public static Currency Usd { get; } = new("USD");

    public static Currency Eur { get; } = new("EUR");

    public static Result<Currency, ValidationError> TryCreate(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Fail<ValidationError>(new Missing(nameof(Currency)));
        }

        var canonical = code.Trim().ToUpperInvariant();
        return IsIsoCode(canonical)
            ? Result.Ok(new Currency(canonical))
            : Result.Fail<ValidationError>(
                new Malformed(nameof(Currency), code, "an ISO 4217 code such as USD"));
    }

    public override string ToString() => Code;

    private static bool IsIsoCode(string? code) =>
        code is { Length: 3 } && code.All(char.IsAsciiLetterUpper);
}
