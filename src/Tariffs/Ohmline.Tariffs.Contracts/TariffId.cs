using System.Text.RegularExpressions;
using Ohmline.SharedKernel;

namespace Ohmline.Tariffs.Contracts;

/// <summary>
/// A tariff, across all its versions: lower-case words joined by hyphens, such as
/// <c>slc-standard</c>. Which version applies is a separate number.
/// </summary>
public readonly partial record struct TariffId : IValueObject<TariffId, string>
{
    private const string Shape = "lower-case words joined by hyphens, like slc-standard";

    public TariffId(string value) => Value = value;

    public string Value
    {
        get;
        init => field = value is not null && Format().IsMatch(value)
            ? value
            : throw new ArgumentException($"'{value}' is not a tariff id.", nameof(Value));
    }

    public static Result<TariffId, ValidationError> TryCreate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Fail<ValidationError>(new Missing(nameof(TariffId)));
        }

        var canonical = value.Trim().ToLowerInvariant();
        return Format().IsMatch(canonical)
            ? Result.Ok(new TariffId(canonical))
            : Result.Fail<ValidationError>(new Malformed(nameof(TariffId), value, Shape));
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Format();
}
