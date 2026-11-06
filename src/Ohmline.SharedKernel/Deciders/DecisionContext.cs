namespace Ohmline.SharedKernel.Deciders;

/// <summary>
/// What a decider is told about the world when it decides: the time, and the feature flags
/// that were on (section 21.8). The shell reads its clock once per command and passes the
/// reading in, so a decider never reads a clock itself, and a test decides what time it is.
/// </summary>
public sealed record DecisionContext(DateTimeOffset Now)
{
    /// <summary>
    /// The flags the shell evaluated for this command; none unless it did. Only Decide sees
    /// them: whatever a flag changed is recorded in the events, which Evolve reads.
    /// </summary>
    public FeatureFlags Features { get; init; } = FeatureFlags.None;

    public static DecisionContext From(TimeProvider clock) => new(clock.GetUtcNow());
}
