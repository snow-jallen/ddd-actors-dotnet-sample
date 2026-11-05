namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// A charging session. A version 7 Guid: unique without coordination, and in creation order to
/// the millisecond, which keeps database indexes and logs readable.
/// </summary>
public readonly record struct SessionId : IValueObject<SessionId, Guid>
{
    public SessionId(Guid value) => Value = value;

    public Guid Value
    {
        get;
        init => field = value.Version == 7
            ? value
            : throw new ArgumentException($"{value} is not a version 7 Guid.", nameof(Value));
    }

    /// <summary>A new id, stamped with the time of the clock that is passed in.</summary>
    public static SessionId New(TimeProvider clock) => new(GuidV7.New(clock));

    public static Result<SessionId, ValidationError> TryCreate(Guid value) =>
        GuidV7.Check(nameof(SessionId), value).Map(valid => new SessionId(valid));

    public override string ToString() => Value.ToString();
}
