using System.Text.RegularExpressions;

namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// A charge point, by the human key painted on it: <c>OHM-</c>, a three-letter area code, a hyphen
/// and four digits, such as <c>OHM-SLC-0042</c>. People read it out on the phone, so it is not a
/// Guid.
/// </summary>
public readonly partial record struct ChargePointId : IValueObject<ChargePointId, string>
{
    private const string Shape = "OHM-, an area code, a hyphen and four digits, like OHM-SLC-0042";

    public ChargePointId(string value) => Value = value;

    public string Value
    {
        get;
        init => field = value is not null && Format().IsMatch(value)
            ? value
            : throw new ArgumentException($"'{value}' is not a charge point id.", nameof(Value));
    }

    public static Result<ChargePointId, ValidationError> TryCreate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Fail<ValidationError>(new Missing(nameof(ChargePointId)));
        }

        var canonical = value.Trim().ToUpperInvariant();
        return Format().IsMatch(canonical)
            ? Result.Ok(new ChargePointId(canonical))
            : Result.Fail<ValidationError>(new Malformed(nameof(ChargePointId), value, Shape));
    }

    public override string ToString() => Value;

    [GeneratedRegex("^OHM-[A-Z]{3}-[0-9]{4}$")]
    private static partial Regex Format();
}
