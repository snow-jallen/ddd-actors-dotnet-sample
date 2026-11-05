namespace Ohmline.SharedKernel;

/// <summary>
/// Why a raw value is not a valid value object. Invalid input is expected, so a smart constructor
/// returns this inside a <see cref="Result{T, E}"/>; it never throws it.
/// </summary>
public union ValidationError(OutOfRange, Malformed, Missing)
{
    /// <summary>A sentence for logs and people. Code decides by the case, never by this.</summary>
    public string Message => this switch
    {
        OutOfRange error => FormattableString.Invariant(
            $"{error.Name} must be {error.Allowed}, not {error.Actual}."),
        Malformed error => $"{error.Name} must be {error.Expected}, not '{error.Input}'.",
        Missing error => $"{error.Name} is missing.",
        null => "This ValidationError is default: it holds no case.",
    };
}

/// <summary>A number outside the values the concept allows.</summary>
public sealed record OutOfRange(string Name, decimal Actual, string Allowed);

/// <summary>Text that does not have the concept's shape.</summary>
public sealed record Malformed(string Name, string Input, string Expected);

/// <summary>No value where one is required: null, empty or only white space.</summary>
public sealed record Missing(string Name);
