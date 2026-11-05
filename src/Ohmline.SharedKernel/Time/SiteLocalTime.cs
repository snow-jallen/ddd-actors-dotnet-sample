namespace Ohmline.SharedKernel.Time;

/// <summary>
/// An instant as the clock on the wall at a site shows it, in the site's time zone (an IANA id
/// such as <c>America/Denver</c>). The instant stays the truth: durations are measured between
/// instants, and the wall-clock time is derived from one. That direction is always safe. The
/// other, from a wall-clock time to an instant, is not: on the night the clocks go forward an
/// hour of wall-clock time never happens, and on the night they go back an hour happens twice.
/// </summary>
public readonly record struct SiteLocalTime
{
    private SiteLocalTime(DateTimeOffset instant, TimeZoneInfo zone)
    {
        Instant = instant.ToUniversalTime();
        TimeZoneId = zone.Id;
        Local = TimeZoneInfo.ConvertTime(instant, zone);
        IsDaylightSavingTime = zone.IsDaylightSavingTime(instant);
    }

    /// <summary>The instant, in UTC.</summary>
    public DateTimeOffset Instant { get; }

    public string TimeZoneId { get; }

    /// <summary>The date and time on the site's wall clock, with the site's offset then.</summary>
    public DateTimeOffset Local { get; }

    public bool IsDaylightSavingTime { get; }

    public DateOnly Date => DateOnly.FromDateTime(Local.DateTime);

    public TimeOnly TimeOfDay => TimeOnly.FromDateTime(Local.DateTime);

    public static Result<SiteLocalTime, ValidationError> TryCreate(
        DateTimeOffset instant, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return Result.Fail<ValidationError>(new Missing("Time zone"));
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
            ? Result.Ok(new SiteLocalTime(instant, zone))
            : Result.Fail<ValidationError>(
                new Malformed("Time zone", timeZoneId, "an IANA time zone like America/Denver"));
    }

    /// <summary>For a time zone known to exist, such as a valid tariff's. Throws if not.</summary>
    public static SiteLocalTime Of(DateTimeOffset instant, string timeZoneId) =>
        TryCreate(instant, timeZoneId)
            .Match(valid => valid, error => throw new ArgumentException(error.Message));

    public override string ToString() =>
        FormattableString.Invariant($"{Local:yyyy-MM-dd HH:mm:ss zzz} {TimeZoneId}");
}
