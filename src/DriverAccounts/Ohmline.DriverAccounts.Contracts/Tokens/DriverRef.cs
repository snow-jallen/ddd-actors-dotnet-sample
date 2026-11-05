using System.Text.RegularExpressions;
using Ohmline.SharedKernel;

namespace Ohmline.DriverAccounts.Contracts.Tokens;

/// <summary>
/// An opaque reference to a driver's account, such as <c>drv-7f3a91c2</c>: never a name or an
/// e-mail address, so that no event anywhere in Ohmline says who charged. Only Driver Accounts
/// may ever map it to a person.
/// </summary>
public readonly partial record struct DriverRef : IValueObject<DriverRef, string>
{
    private const string Shape = "drv- and 6 to 32 lower-case letters or digits, like drv-7f3a91c2";

    public DriverRef(string value) => Value = value;

    public string Value
    {
        get;
        init => field = value is not null && Format().IsMatch(value)
            ? value
            : throw new ArgumentException($"'{value}' is not a driver reference.", nameof(Value));
    }

    public static Result<DriverRef, ValidationError> TryCreate(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Fail<ValidationError>(new Missing(nameof(DriverRef)))
            : Format().IsMatch(value)
                ? Result.Ok(new DriverRef(value))
                : Result.Fail<ValidationError>(new Malformed(nameof(DriverRef), value, Shape));

    public override string ToString() => Value;

    [GeneratedRegex("^drv-[a-z0-9]{6,32}$")]
    private static partial Regex Format();
}
