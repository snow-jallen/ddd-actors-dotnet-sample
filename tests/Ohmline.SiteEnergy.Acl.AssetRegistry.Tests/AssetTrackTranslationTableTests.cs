using System.Globalization;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Acl.AssetRegistry.Tests;

/// <summary>
/// The translation rules of <c>docs/acl/assettrack.md</c> as a table: one AssetTrack column, what
/// someone typed into it, and what Site Energy reads, or the rule that refuses it. Every other
/// field of the row is a clean, active connector at the depot, so each row tests one value.
/// </summary>
public sealed class AssetTrackTranslationTableTests
{
    private static readonly AssetTrackRow CleanRow = new(
        2, "AT-10041", "SLC-DEPOT", "Salt Lake Depot", "200 kW", "10 kW", "America/Denver",
        "OHM-SLC-0041", "1", "6 kW", "50 kW", "FLEET", "ACTIVE", "2025-02-11");

    public static readonly TheoryData<string, string, string> Values = new()
    {
        // Column            AssetTrack says            Site Energy reads
        { "STATUS",         " active ",                "a connector in use" },
        { "STATUS",         "Retired",                 "skipped by Status" },
        { "STATUS",         "TEST",                    "skipped by Status" },
        { "STATUS",         "Decommisioned",           "rejected by Status" },
        { "STATUS",         "",                        "rejected by Status" },
        { "SITE_CODE",      "slc depot",               "slc-depot" },
        { "SITE_CODE",      " SLC__DEPOT ",            "slc-depot" },
        { "SITE_CODE",      "",                        "rejected by SiteCode" },
        { "GRID_CONN",      "200 KW",                  "200 kW" },
        { "GRID_CONN",      "200000",                  "200 kW" },
        { "GRID_CONN",      "0.2MW",                   "200 kW" },
        { "GRID_CONN",      "1,000 kW",                "rejected by PowerValue" },
        { "GRID_CONN",      "200 kVA",                 "rejected by PowerValue" },
        { "GRID_CONN",      "-200 kW",                 "rejected by PowerValue" },
        { "GRID_CONN",      "9 kW",                    "rejected by Plausibility" },
        { "GRID_CONN",      "11 MW",                   "rejected by Plausibility" },
        { "MARGIN",         "0",                       "0 kW" },
        { "MARGIN",         "200 kW",                  "rejected by Plausibility" },
        { "MIN_PWR",        "11000",                   "11 kW" },
        { "MIN_PWR",        "11",                      "rejected by Plausibility" },
        { "MIN_PWR",        "60 kW",                   "rejected by Plausibility" },
        { "MAX_PWR",        "401 kW",                  "rejected by Plausibility" },
        { "TZ",             "america/denver",          "America/Denver" },
        { "TZ",             "US/Mountain",             "America/Denver" },
        { "TZ",             "Mountain Standard Time",  "rejected by TimeZone" },
        { "TZ",             "",                        "rejected by TimeZone" },
        { "CHARGER_SERIAL", "OHMSLC0041",              "OHM-SLC-0041/1" },
        { "CHARGER_SERIAL", " ohm-slc-0041 ",          "OHM-SLC-0041/1" },
        { "CHARGER_SERIAL", "SLC-0041",                "rejected by ChargePoint" },
        { "CHARGER_SERIAL", "",                        "rejected by ChargePoint" },
        { "PLUG_NO",        "#2",                      "OHM-SLC-0041/2" },
        { "PLUG_NO",        "0",                       "rejected by ConnectorNumber" },
        { "PLUG_NO",        "+1",                      "rejected by ConnectorNumber" },
        { "PLUG_NO",        "two",                     "rejected by ConnectorNumber" },
        { "PRIO",           "f",                       "Fleet" },
        { "PRIO",           "",                        "Public" },
        { "PRIO",           "Flet",                    "rejected by PriorityClass" },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void Given_a_value_as_assettrack_writes_it_then_site_energy_reads_it_or_refuses_it(
        string column, string written, string read)
    {
        var outcome = AssetTrackTranslator.TranslateRow(With(column, written));

        Describe(column, outcome).ShouldBe(read);
    }

    [Fact]
    public void Given_the_table_then_every_column_that_decides_something_has_a_row()
    {
        var columns = Values.Select(row => (string)row.Data.Item1).Distinct();

        columns.ShouldBe(
        [
            "STATUS", "SITE_CODE", "GRID_CONN", "MARGIN", "MIN_PWR", "MAX_PWR", "TZ",
            "CHARGER_SERIAL", "PLUG_NO", "PRIO",
        ]);
    }

    private static AssetTrackRow With(string column, string written) => column switch
    {
        "STATUS" => CleanRow with { Status = written },
        "SITE_CODE" => CleanRow with { SiteCode = written },
        "GRID_CONN" => CleanRow with { GridConnection = written },
        "MARGIN" => CleanRow with { Margin = written },
        "MIN_PWR" => CleanRow with { MinPower = written },
        "MAX_PWR" => CleanRow with { MaxPower = written },
        "TZ" => CleanRow with { TimeZone = written },
        "CHARGER_SERIAL" => CleanRow with { ChargerSerial = written },
        "PLUG_NO" => CleanRow with { PlugNumber = written },
        "PRIO" => CleanRow with { Priority = written },
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, null),
    };

    /// <summary>What Site Energy read from the column, or the rule that decided the row.</summary>
    private static string Describe(string column, AssetRowTranslation outcome) => outcome switch
    {
        Translated translated => column switch
        {
            "STATUS" => "a connector in use",
            "SITE_CODE" => translated.Site.SiteId.ToString(),
            "GRID_CONN" => Kilowatts(translated.Site.GridLimit),
            "MARGIN" => Kilowatts(translated.Site.SafetyMargin),
            "MIN_PWR" => Kilowatts(translated.Connector.MinPower),
            "MAX_PWR" => Kilowatts(translated.Connector.MaxPower),
            "TZ" => translated.Site.TimeZoneId,
            "CHARGER_SERIAL" or "PLUG_NO" => translated.Connector.ConnectorId.ToString(),
            "PRIO" => translated.Connector.Priority.GetType().Name,
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, null),
        },
        Skipped skipped => $"skipped by {skipped.Rule}",
        Rejected rejected => $"rejected by {rejected.Rule}",
        null => "nothing",
    };

    private static string Kilowatts(Power power) =>
        $"{power.Kilowatts.ToString("0.###", CultureInfo.InvariantCulture)} kW";
}
