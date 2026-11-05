using System.Text.RegularExpressions;

namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// An id token: the UID of an RFID card (or an app credential issued like one), as 4 to 10 bytes
/// in upper-case hexadecimal, such as <c>04A2B3C4D5E6F7</c>. Readers report the same card in
/// either case, so TryCreate upper-cases what it is given; the constructor takes only upper case.
/// </summary>
public readonly partial record struct TokenUid : IValueObject<TokenUid, string>
{
    private const string Shape = "4 to 10 bytes of hexadecimal, like 04A2B3C4";

    public TokenUid(string value) => Value = value;

    public string Value
    {
        get;
        init => field = value is not null && Format().IsMatch(value)
            ? value
            : throw new ArgumentException($"'{value}' is not a token UID.", nameof(Value));
    }

    public static Result<TokenUid, ValidationError> TryCreate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Fail<ValidationError>(new Missing(nameof(TokenUid)));
        }

        var canonical = value.Trim().ToUpperInvariant();
        return Format().IsMatch(canonical)
            ? Result.Ok(new TokenUid(canonical))
            : Result.Fail<ValidationError>(new Malformed(nameof(TokenUid), value, Shape));
    }

    public override string ToString() => Value;

    [GeneratedRegex("^([0-9A-F]{2}){4,10}$")]
    private static partial Regex Format();
}
