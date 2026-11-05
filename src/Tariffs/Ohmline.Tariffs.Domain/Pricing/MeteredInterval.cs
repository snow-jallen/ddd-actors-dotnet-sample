using Ohmline.SharedKernel.Units;

namespace Ohmline.Tariffs.Domain.Pricing;

/// <summary>The energy a charge point metered between two of its readings.</summary>
public readonly record struct MeteredInterval
{
    public MeteredInterval(DateTimeOffset from, DateTimeOffset until, Energy energy)
    {
        From = from;
        Until = until > from
            ? until
            : throw new ArgumentOutOfRangeException(
                nameof(until), until, "An interval ends after it starts.");
        Energy = energy;
    }

    public DateTimeOffset From { get; }

    public DateTimeOffset Until { get; }

    public Energy Energy { get; }

    public TimeSpan Length => Until - From;
}
