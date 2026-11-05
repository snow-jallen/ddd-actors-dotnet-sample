namespace Ch03.LanguageLab.Tests;

/// <summary>A reservation that runs out. It reads the time only through the clock.</summary>
public sealed class ReservationTimer(TimeProvider clock, TimeSpan holdFor)
{
    public DateTimeOffset ExpiresAt { get; } = clock.GetUtcNow() + holdFor;

    public bool HasExpired => clock.GetUtcNow() >= ExpiresAt;
}
