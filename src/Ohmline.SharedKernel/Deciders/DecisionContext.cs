namespace Ohmline.SharedKernel.Deciders;

/// <summary>
/// What a decider is told about the world when it decides: for now, only the time. The shell
/// reads its clock once per command and passes the reading in, so a decider never reads a clock
/// itself, and a test decides what time it is.
/// </summary>
public sealed record DecisionContext(DateTimeOffset Now)
{
    public static DecisionContext From(TimeProvider clock) => new(clock.GetUtcNow());
}
