using System.Collections.Immutable;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Domain.Sites;

/// <summary>
/// A site as Site Energy needs it: the grid connection limit, the safety margin kept below it,
/// the site's time zone, and its connectors. A value object: two configurations with the same
/// values are equal, which a record alone would not give (ImmutableArray compares by reference).
/// </summary>
public sealed record SiteConfiguration
{
    private SiteConfiguration(
        SiteId siteId,
        Power gridLimit,
        Power safetyMargin,
        string timeZoneId,
        ImmutableArray<ConnectorConfiguration> connectors)
    {
        SiteId = siteId;
        GridLimit = gridLimit;
        SafetyMargin = safetyMargin;
        TimeZoneId = timeZoneId;
        Connectors = connectors;
    }

    public SiteId SiteId { get; }

    /// <summary>The most the site may draw from the grid.</summary>
    public Power GridLimit { get; }

    public Power SafetyMargin { get; }

    /// <summary>The IANA id of the site's time zone, such as <c>America/Denver</c>.</summary>
    public string TimeZoneId { get; }

    public ImmutableArray<ConnectorConfiguration> Connectors { get; }

    /// <summary>
    /// The grid connection limit minus the safety margin. The effective capacity (section 7.8)
    /// subtracts any curtailment from this as well.
    /// </summary>
    public Power ConfiguredCapacity => GridLimit - SafetyMargin;

    public static Result<SiteConfiguration, ValidationError> TryCreate(
        SiteId siteId,
        Power gridLimit,
        Power safetyMargin,
        string timeZoneId,
        IEnumerable<ConnectorConfiguration> connectors)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return Result.Fail<ValidationError>(new Missing("Time zone"));
        }

        if (safetyMargin >= gridLimit)
        {
            return Result.Fail<ValidationError>(new OutOfRange(
                "Safety margin", safetyMargin.Kilowatts, $"below the grid limit of {gridLimit}"));
        }

        var site = new SiteConfiguration(
            siteId, gridLimit, safetyMargin, timeZoneId, [.. connectors]);
        return site.CheckConnectors();
    }

    /// <summary>For values known to be valid. An invalid value is a bug, so it throws.</summary>
    public static SiteConfiguration Create(
        SiteId siteId,
        Power gridLimit,
        Power safetyMargin,
        string timeZoneId,
        IEnumerable<ConnectorConfiguration> connectors) =>
        TryCreate(siteId, gridLimit, safetyMargin, timeZoneId, connectors)
            .Match(valid => valid, error => throw new ArgumentException(error.Message));

    public bool Equals(SiteConfiguration? other) =>
        other is not null
        && SiteId == other.SiteId
        && GridLimit == other.GridLimit
        && SafetyMargin == other.SafetyMargin
        && TimeZoneId == other.TimeZoneId
        && Connectors.SequenceEqual(other.Connectors);

    public override int GetHashCode() =>
        HashCode.Combine(SiteId, GridLimit, SafetyMargin, TimeZoneId, Connectors.Length);

    private Result<SiteConfiguration, ValidationError> CheckConnectors()
    {
        var seen = new HashSet<ConnectorId>();
        foreach (var connector in Connectors)
        {
            if (!seen.Add(connector.ConnectorId))
            {
                return Result.Fail<ValidationError>(new Malformed(
                    "Connectors", $"{connector.ConnectorId}", "each connector listed once"));
            }

            if (connector.MaxPower > ConfiguredCapacity)
            {
                return Result.Fail<ValidationError>(new OutOfRange(
                    $"Maximum power of {connector.ConnectorId}",
                    connector.MaxPower.Kilowatts,
                    $"at most the configured capacity of {ConfiguredCapacity}"));
            }
        }

        return Result.Ok(this);
    }
}
