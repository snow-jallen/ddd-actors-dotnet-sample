using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Acl.AssetRegistry;

/// <summary>
/// Turns AssetTrack rows into site configurations, following <c>docs/acl/assettrack.md</c>. A pure
/// function: it reads no clock and changes nothing, and it never guesses. A value it cannot read
/// with certainty rejects the row, with a reason a person can act on.
/// </summary>
public static partial class AssetTrackTranslator
{
    private static readonly Dictionary<string, string> TimeZoneAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mountain"] = "America/Denver",
            ["US/Mountain"] = "America/Denver",
            ["MST7MDT"] = "America/Denver",
        };

    public static AssetTrackImport Translate(IEnumerable<AssetTrackRow> rows)
    {
        ImmutableArray<AssetRowTranslation> outcomes = [.. rows.Select(TranslateRow)];
        outcomes = Overrule(outcomes, Duplicates(TranslatedRows(outcomes)));
        outcomes = Overrule(outcomes, SiteDisagreements(TranslatedRows(outcomes)));
        outcomes = Overrule(outcomes, OverCapacity(TranslatedRows(outcomes)));
        return new AssetTrackImport(outcomes, Sites(TranslatedRows(outcomes)));
    }

    /// <summary>Rules 1 to 8: everything that one row decides on its own.</summary>
    public static AssetRowTranslation TranslateRow(AssetTrackRow row) =>
        row.Status.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => ReadActiveRow(row) switch
            {
                Ok<Translated> translated => translated.Value,
                Err<Rejection> rejection =>
                    new Rejected(row, rejection.Error.Rule, rejection.Error.Reason),
                null => throw new InvalidOperationException("ReadActiveRow returned default."),
            },
            "DECOMMISSIONED" or "RETIRED" or "TEST" =>
                new Skipped(row, AssetTrackRule.Status, $"Status {row.Status.Trim()}"),
            _ => new Rejected(row, AssetTrackRule.Status, $"Unknown status '{row.Status}'"),
        };

    private static Result<Translated, Rejection> ReadActiveRow(AssetTrackRow row) =>
        ReadSiteId(row.SiteCode).Bind(siteId =>
            ReadPowers(row).Bind(CheckPlausibility).Bind(powers =>
                ReadTimeZone(row.TimeZone).Bind(timeZone =>
                    ReadConnectorId(row.ChargerSerial, row.PlugNumber).Bind(connectorId =>
                        ReadPriority(row.Priority).Map(priority => new Translated(
                            row,
                            new SiteSettings(
                                siteId, powers.GridLimit, powers.SafetyMargin, timeZone),
                            ConnectorConfiguration.Create(
                                connectorId, powers.MinPower, powers.MaxPower, priority)))))));

    private static Result<SiteId, Rejection> ReadSiteId(string siteCode) =>
        SiteId.TryCreate(SpacesAndUnderscores().Replace(siteCode.Trim(), "-"))
            .Under(AssetTrackRule.SiteCode);

    private static Result<PowerFigures, Rejection> ReadPowers(AssetTrackRow row) =>
        ReadPower("Grid connection", row.GridConnection).Bind(grid =>
            ReadPower("Margin", row.Margin).Bind(margin =>
                ReadPower("Minimum power", row.MinPower).Bind(min =>
                    ReadPower("Maximum power", row.MaxPower).Map(max =>
                        new PowerFigures(grid, margin, min, max)))));

    /// <summary>Rule 3: a number with a decimal point and maybe a unit; no unit means W.</summary>
    private static Result<Power, Rejection> ReadPower(string column, string text)
    {
        var value = text.Trim();
        if (value.Contains(','))
        {
            return Reject(AssetTrackRule.PowerValue,
                $"{column} '{value}' has a comma: a decimal comma or a thousands separator?");
        }

        var match = PowerValue().Match(value);
        if (!match.Success)
        {
            return Reject(AssetTrackRule.PowerValue, $"{column} '{value}' is not a power");
        }

        var number = decimal.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
        return match.Groups["unit"].Value.ToUpperInvariant() switch
        {
            "" or "W" => Result.Ok(Power.FromWatts(number)),
            "KW" => Result.Ok(new Power(number)),
            "MW" => Result.Ok(new Power(number * 1000m)),
            var unit => throw new InvalidOperationException($"The pattern let '{unit}' through."),
        };
    }

    /// <summary>Rule 4: values that parse but cannot be right.</summary>
    private static Result<PowerFigures, Rejection> CheckPlausibility(PowerFigures powers) =>
        powers switch
        {
            { GridLimit.Kilowatts: < 10m or > 10_000m } => Reject(AssetTrackRule.Plausibility,
                $"Grid connection {powers.GridLimit} is outside 10 kW to 10 MW"),
            _ when powers.SafetyMargin >= powers.GridLimit => Reject(AssetTrackRule.Plausibility,
                $"Margin {powers.SafetyMargin} is not below the grid connection"),
            { MinPower.Kilowatts: < 1m or > 400m } => Reject(AssetTrackRule.Plausibility,
                $"Minimum power {powers.MinPower} is outside 1 kW to 400 kW"),
            { MaxPower.Kilowatts: < 1m or > 400m } => Reject(AssetTrackRule.Plausibility,
                $"Maximum power {powers.MaxPower} is outside 1 kW to 400 kW"),
            _ when powers.MinPower > powers.MaxPower => Reject(AssetTrackRule.Plausibility,
                $"Minimum power {powers.MinPower} is above maximum {powers.MaxPower}"),
            _ => Result.Ok(powers),
        };

    /// <summary>Rule 5: an IANA id in any case, or one of the old drop-down's aliases.</summary>
    private static Result<string, Rejection> ReadTimeZone(string text)
    {
        var value = text.Trim();
        if (TimeZoneAliases.TryGetValue(value, out var alias))
        {
            return Result.Ok(alias);
        }

        // TryFindSystemTimeZoneById is case-sensitive on Linux, so look through the list first.
        var zone = TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(
            known => known.HasIanaId && known.Id.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (zone is not null
            || (TimeZoneInfo.TryFindSystemTimeZoneById(value, out zone) && zone.HasIanaId))
        {
            return Result.Ok(zone.Id);
        }

        return Reject(AssetTrackRule.TimeZone, $"Time zone '{value}' is not an IANA time zone id");
    }

    /// <summary>Rules 6 and 7: the charge point (hyphens restored) and the plug number.</summary>
    private static Result<ConnectorId, Rejection> ReadConnectorId(string serial, string plug)
    {
        var chargePoint = UnhyphenatedChargePoint().Replace(serial.Trim(), "OHM-$1-$2");
        var number = plug.Trim() is ['#', .. var digits] ? digits : plug.Trim();

        return ChargePointId.TryCreate(chargePoint).Under(AssetTrackRule.ChargePoint)
            .Bind(chargePointId =>
                int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                    ? ConnectorId.TryCreate(chargePointId, n).Under(AssetTrackRule.ConnectorNumber)
                    : Reject(AssetTrackRule.ConnectorNumber, $"Plug number '{plug}' is no number"));
    }

    /// <summary>Rule 8: a blank priority is Public; a misspelling is never corrected.</summary>
    private static Result<PriorityClass, Rejection> ReadPriority(string text) =>
        text.Trim().ToUpperInvariant() switch
        {
            "FLEET" or "F" => Result.Ok<PriorityClass>(new Fleet()),
            "PUBLIC" or "P" or "" => Result.Ok<PriorityClass>(new Public()),
            _ => Reject(AssetTrackRule.PriorityClass,
                $"Priority '{text.Trim()}' is neither Fleet nor Public"),
        };

    /// <summary>Rule 9: identical rows count once; rows that disagree are all rejected.</summary>
    private static IEnumerable<AssetRowTranslation> Duplicates(IEnumerable<Translated> rows) =>
        from translated in rows
        group translated by translated.Connector.ConnectorId into connector
        where connector.Count() > 1
        let first = connector.First()
        let identical = connector.All(
            row => row.Site == first.Site && row.Connector == first.Connector)
        from duplicate in identical ? connector.Skip(1) : connector
        select identical
            ? (AssetRowTranslation)new Skipped(duplicate.Row, AssetTrackRule.Duplicate,
                $"The same connector and values as line {first.Row.Line}")
            : new Rejected(duplicate.Row, AssetTrackRule.Duplicate,
                $"Lines {Lines(connector)} disagree about connector {connector.Key}");

    /// <summary>Rule 10: one site, one grid connection, one margin, one time zone.</summary>
    private static IEnumerable<AssetRowTranslation> SiteDisagreements(
        IEnumerable<Translated> rows) =>
        from translated in rows
        group translated by translated.Site.SiteId into site
        where site.Select(row => row.Site).Distinct().Count() > 1
        from row in site
        select (AssetRowTranslation)new Rejected(row.Row, AssetTrackRule.SiteAgreement,
            $"Lines {Lines(site)} disagree about the grid connection, margin or time zone"
            + $" of {site.Key}");

    /// <summary>Rule 11: no connector may need more than the site can give.</summary>
    private static IEnumerable<AssetRowTranslation> OverCapacity(IEnumerable<Translated> rows) =>
        from row in rows
        let capacity = row.Site.GridLimit - row.Site.SafetyMargin
        where row.Connector.MaxPower > capacity
        select (AssetRowTranslation)new Rejected(row.Row, AssetTrackRule.EffectiveCapacity,
            $"Maximum power {row.Connector.MaxPower} is above the site's {capacity}");

    private static ImmutableArray<SiteConfiguration> Sites(IEnumerable<Translated> rows) =>
    [
        .. from translated in rows
           group translated by translated.Site into site
           select SiteConfiguration.Create(
               site.Key.SiteId,
               site.Key.GridLimit,
               site.Key.SafetyMargin,
               site.Key.TimeZoneId,
               site.Select(row => row.Connector)),
    ];

    private static ImmutableArray<AssetRowTranslation> Overrule(
        ImmutableArray<AssetRowTranslation> outcomes, IEnumerable<AssetRowTranslation> verdicts)
    {
        var verdictByLine = verdicts.ToDictionary(verdict => verdict.Row.Line);
        return
        [
            .. outcomes.Select(
                outcome => verdictByLine.GetValueOrDefault(outcome.Row.Line, outcome)),
        ];
    }

    private static IEnumerable<Translated> TranslatedRows(
        ImmutableArray<AssetRowTranslation> outcomes) =>
        outcomes.Select(outcome => outcome.Value).OfType<Translated>();

    private static string Lines(IEnumerable<Translated> rows) =>
        string.Join(", ", rows.Select(row => row.Row.Line));

    private static Err<Rejection> Reject(AssetTrackRule rule, string reason) =>
        Result.Fail(new Rejection(rule, reason));

    extension<T>(Result<T, ValidationError> result)
    {
        /// <summary>Reports a kernel validation error as a rejection under one rule.</summary>
        private Result<T, Rejection> Under(AssetTrackRule rule) => result switch
        {
            Ok<T> ok => ok,
            Err<ValidationError> invalid => Reject(rule, invalid.Error.Message),
            null => throw new InvalidOperationException("This Result is default."),
        };
    }

    [GeneratedRegex("[ _]+")]
    private static partial Regex SpacesAndUnderscores();

    [GeneratedRegex(@"^(?<number>[0-9]+(\.[0-9]+)?) ?(?<unit>w|kw|mw)?$", RegexOptions.IgnoreCase)]
    private static partial Regex PowerValue();

    [GeneratedRegex("^OHM([A-Z]{3})([0-9]{4})$", RegexOptions.IgnoreCase)]
    private static partial Regex UnhyphenatedChargePoint();

    private sealed record Rejection(AssetTrackRule Rule, string Reason);

    private sealed record PowerFigures(
        Power GridLimit, Power SafetyMargin, Power MinPower, Power MaxPower);
}
