namespace Ohmline.SharedKernel.Identity;

/// <summary>
/// Time-ordered identifiers (RFC 9562 version 7). The time comes from the injected clock, never
/// from DateTime.UtcNow, so tests decide what it is.
/// </summary>
public static class GuidV7
{
    public static Guid New(TimeProvider clock) => Guid.CreateVersion7(clock.GetUtcNow());
}
