namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// Time-ordered identifiers (RFC 9562 version 7). The time comes from the injected clock, never
/// from DateTime.UtcNow, so tests decide what it is.
/// </summary>
public static class GuidV7
{
    public static Guid New(TimeProvider clock) => Guid.CreateVersion7(clock.GetUtcNow());

    /// <summary>The check behind the TryCreate of each Guid id; the name says which id.</summary>
    public static Result<Guid, ValidationError> Check(string name, Guid value) => value switch
    {
        { Version: 7 } => Result.Ok(value),
        _ when value == Guid.Empty => Result.Fail<ValidationError>(new Missing(name)),
        _ => Result.Fail<ValidationError>(new Malformed(name, $"{value}", "a version 7 Guid")),
    };
}
