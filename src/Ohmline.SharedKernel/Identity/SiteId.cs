using System.Text.RegularExpressions;

namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// A site: a location with one grid connection. Lower-case kebab case that starts with a
/// three-letter area code, such as <c>slc-depot</c>.
/// </summary>
public readonly partial record struct SiteId : IValueObject<SiteId, string>
{
    private const string Shape = "an area code and hyphenated words, like slc-depot";

    public SiteId(string value) => Value = value;

    public string Value
    {
        get;
        init => field = value is not null && Format().IsMatch(value)
            ? value
            : throw new ArgumentException($"'{value}' is not a site id.", nameof(Value));
    }

    public static Result<SiteId, ValidationError> TryCreate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Fail<ValidationError>(new Missing(nameof(SiteId)));
        }

        var canonical = value.Trim().ToLowerInvariant();
        return Format().IsMatch(canonical)
            ? Result.Ok(new SiteId(canonical))
            : Result.Fail<ValidationError>(new Malformed(nameof(SiteId), value, Shape));
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z]{3}(-[a-z0-9]+)+$")]
    private static partial Regex Format();
}
