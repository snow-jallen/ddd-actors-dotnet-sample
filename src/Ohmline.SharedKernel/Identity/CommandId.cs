namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// One command, as its sender issued it. A version 7 Guid created by the sender, so a command
/// that is sent twice (a retry after a timeout) carries the same id both times. Every event a
/// command causes records this id: it is the event's causation.
/// </summary>
public readonly record struct CommandId : IValueObject<CommandId, Guid>
{
    public CommandId(Guid value) => Value = value;

    public Guid Value
    {
        get;
        init => field = value.Version == 7
            ? value
            : throw new ArgumentException($"{value} is not a version 7 Guid.", nameof(Value));
    }

    /// <summary>A new id, stamped with the time of the clock that is passed in.</summary>
    public static CommandId New(TimeProvider clock) => new(GuidV7.New(clock));

    /// <summary>
    /// The id of a command that follows from another, such as the release a subscriber sends
    /// when a session ends: derived from the cause's id and the command's purpose, so whoever
    /// sends it again sends it with the same id, and its receiver deduplicates it (section 15.1).
    /// </summary>
    public static CommandId Derive(CommandId cause, string purpose) =>
        new(GuidV7.Derive(cause.Value, purpose));

    public static Result<CommandId, ValidationError> TryCreate(Guid value) =>
        GuidV7.Check(nameof(CommandId), value).Map(valid => new CommandId(valid));

    public override string ToString() => Value.ToString();
}
