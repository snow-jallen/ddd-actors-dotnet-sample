namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// A charging journey: the process from "start charging" to a settled payment. A version 7
/// Guid, like <see cref="SessionId"/>, but never interchangeable with one.
/// </summary>
public readonly record struct JourneyId : IValueObject<JourneyId, Guid>
{
    public JourneyId(Guid value) => Value = value;

    public Guid Value
    {
        get;
        init => field = value.Version == 7
            ? value
            : throw new ArgumentException($"{value} is not a version 7 Guid.", nameof(Value));
    }

    public static JourneyId New(TimeProvider clock) => new(GuidV7.New(clock));

    public static Result<JourneyId, ValidationError> TryCreate(Guid value) =>
        GuidV7.Check(nameof(JourneyId), value).Map(valid => new JourneyId(valid));

    public override string ToString() => Value.ToString();
}
