namespace Ohmline.Tariffs.Contracts;

/// <summary>
/// A window of the day, in the site's local wall-clock time, in which a rate applies: from
/// <paramref name="From"/> up to, not including, <paramref name="Until"/>. A band that ends at
/// or before its start runs past midnight, so 22:00 to 06:00 is the night.
/// </summary>
public sealed record TimeOfUseBand(string Name, TimeOnly From, TimeOnly Until)
{
    public bool Contains(TimeOnly localTime) => localTime.IsBetween(From, Until);

    /// <summary>Two bands overlap when either one contains the other's start.</summary>
    public bool Overlaps(TimeOfUseBand other) => Contains(other.From) || other.Contains(From);

    public override string ToString() => $"{Name} ({From:HH:mm} to {Until:HH:mm})";
}
