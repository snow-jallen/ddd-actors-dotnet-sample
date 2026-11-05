namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// What a projection does about an event it keeps failing on (section 13.7). Either way it
/// first tries again, with backoff, <c>MaxRetries</c> times: a deadlock or a timeout deserves
/// another go. A database that cannot be reached at all never counts against the event; the
/// stream backs off and waits for it.
/// </summary>
public union ProjectionFailurePolicy(StopAfterRetries, RecordAndSkip)
{
    public int MaxRetries => this switch
    {
        StopAfterRetries stop => stop.MaxRetries,
        RecordAndSkip skip => skip.MaxRetries,
        null => 0,
    };
}

/// <summary>
/// Then stop, loudly, at that event: the read model stays correct up to it and stops moving.
/// For read models that must never be wrong. Fix the projection and restart the node; the
/// journal still has every event.
/// </summary>
public sealed record StopAfterRetries(int MaxRetries);

/// <summary>
/// Then write the event down in <c>read_models.projection_failures</c>, in the same transaction
/// as the offset that moves past it, and go on. For read models that must keep moving, where
/// one missing event is better than a frozen screen.
/// </summary>
public sealed record RecordAndSkip(int MaxRetries);
