using System.Globalization;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Acl.AssetRegistry.Tests;

public sealed class AssetTrackTranslatorTests
{
    private static readonly AssetTrackImport Import = AssetTrackTranslator.Translate(Export.Rows());

    private static readonly AssetTrackRow ActiveRow = new(
        2, "AT-10041", "SLC-DEPOT", "Salt Lake Depot", "200 kW", "10 kW", "America/Denver",
        "OHM-SLC-0041", "1", "6 kW", "50 kW", "FLEET", "ACTIVE", "2025-02-11");

    [Fact]
    public void Given_the_committed_export_then_every_row_has_exactly_one_outcome()
    {
        Import.Rows.Length.ShouldBe(40);
        Import.Rows.Count(row => row is Translated).ShouldBe(19);
        Import.Rows.Count(row => row is Skipped).ShouldBe(5);
        Import.Rows.Count(row => row is Rejected).ShouldBe(16);
    }

    [Fact]
    public void Given_the_committed_export_then_four_sites_are_configured()
    {
        Import.Sites.Select(site => $"{site.SiteId} {site.Connectors.Length}").ShouldBe(
            ["slc-depot 5", "slc-west-garage 6", "ogd-library 3", "slc-fleet-yard 5"]);
    }

    [Fact]
    public void Given_the_salt_lake_depot_in_three_spellings_then_one_configuration_results()
    {
        var expected = SiteConfiguration.Create(
            new SiteId("slc-depot"), new Power(200m), new Power(10m), "America/Denver",
            [
                Connector("OHM-SLC-0041", 1, new Fleet()),
                Connector("OHM-SLC-0041", 2, new Fleet()),
                Connector("OHM-SLC-0042", 2, new Fleet()),
                Connector("OHM-SLC-0043", 1, new Fleet()),
                Connector("OHM-SLC-0043", 2, new Public()),
            ]);

        Import.Sites.Single(site => site.SiteId == new SiteId("slc-depot")).ShouldBe(expected);
    }

    [Theory]
    [InlineData(2, "Translated")]
    [InlineData(3, "Translated")]
    [InlineData(4, "Rejected by TimeZone")]
    [InlineData(5, "Translated")]
    [InlineData(6, "Translated")]
    [InlineData(7, "Translated")]
    [InlineData(8, "Skipped by Duplicate")]
    [InlineData(9, "Translated")]
    [InlineData(10, "Translated")]
    [InlineData(11, "Translated")]
    [InlineData(12, "Rejected by Plausibility")]
    [InlineData(13, "Translated")]
    [InlineData(14, "Translated")]
    [InlineData(15, "Rejected by PriorityClass")]
    [InlineData(16, "Translated")]
    [InlineData(17, "Translated")]
    [InlineData(18, "Translated")]
    [InlineData(19, "Rejected by Plausibility")]
    [InlineData(20, "Rejected by PowerValue")]
    [InlineData(21, "Skipped by Status")]
    [InlineData(22, "Rejected by SiteAgreement")]
    [InlineData(23, "Rejected by SiteAgreement")]
    [InlineData(24, "Rejected by SiteAgreement")]
    [InlineData(25, "Rejected by SiteAgreement")]
    [InlineData(26, "Skipped by Status")]
    [InlineData(27, "Rejected by Status")]
    [InlineData(28, "Skipped by Status")]
    [InlineData(29, "Skipped by Status")]
    [InlineData(30, "Translated")]
    [InlineData(31, "Translated")]
    [InlineData(32, "Rejected by PriorityClass")]
    [InlineData(33, "Translated")]
    [InlineData(34, "Rejected by Plausibility")]
    [InlineData(35, "Translated")]
    [InlineData(36, "Rejected by Duplicate")]
    [InlineData(37, "Rejected by Duplicate")]
    [InlineData(38, "Translated")]
    [InlineData(39, "Rejected by ChargePoint")]
    [InlineData(40, "Rejected by ConnectorNumber")]
    [InlineData(41, "Translated")]
    public void Given_a_line_of_the_committed_export_then_its_outcome_is_the_documented_one(
        int line, string outcome)
    {
        Describe(Import.Rows.Single(row => row.Row.Line == line)).ShouldBe(outcome);
    }

    [Fact]
    public void Given_a_rejected_row_then_the_reason_tells_a_person_what_to_fix()
    {
        var rejections = Import.Rows.Select(row => row.Value).OfType<Rejected>().ToList();

        rejections.Single(row => row.Row.Line == 15).Reason
            .ShouldBe("Priority 'Pubic' is neither Fleet nor Public");
        rejections.Single(row => row.Row.Line == 4).Reason
            .ShouldBe("Time zone 'America/Denvr' is not an IANA time zone id");
        rejections.Single(row => row.Row.Line == 36).Reason
            .ShouldBe("Lines 36, 37 disagree about connector OHM-SLC-0304/1");
    }

    [Theory]
    [InlineData("200 kW", "200")]
    [InlineData("200kw", "200")]
    [InlineData("200000W", "200")]
    [InlineData("200000", "200")]
    [InlineData("0.4 MW", "400")]
    [InlineData("12500 W", "12.5")]
    public void Given_a_power_as_assettrack_writes_it_then_it_is_read_in_kilowatts(
        string text, string kilowatts)
    {
        var outcome = AssetTrackTranslator.TranslateRow(ActiveRow with { GridConnection = text });

        outcome.Value.ShouldBeOfType<Translated>().Site.GridLimit
            .ShouldBe(new Power(decimal.Parse(kilowatts, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Given_a_decimal_comma_then_the_translator_does_not_guess()
    {
        var outcome = AssetTrackTranslator.TranslateRow(ActiveRow with { MinPower = "3,7 kW" });

        outcome.Value.ShouldBeOfType<Rejected>().Reason.ShouldBe(
            "Minimum power '3,7 kW' has a comma: a decimal comma or a thousands separator?");
    }

    [Theory]
    [InlineData("Mountain")]
    [InlineData("us/mountain")]
    [InlineData("MST7MDT")]
    public void Given_an_alias_of_the_old_drop_down_then_the_time_zone_is_america_denver(
        string zone)
    {
        var outcome = AssetTrackTranslator.TranslateRow(ActiveRow with { TimeZone = zone });

        outcome.Value.ShouldBeOfType<Translated>().Site.TimeZoneId.ShouldBe("America/Denver");
    }

    [Fact]
    public void Given_an_iana_id_in_lower_case_then_the_canonical_id_is_kept()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Verified on Linux's time zone database.");

        var outcome = AssetTrackTranslator.TranslateRow(
            ActiveRow with { TimeZone = "america/boise" });

        outcome.Value.ShouldBeOfType<Translated>().Site.TimeZoneId.ShouldBe("America/Boise");
    }

    [Fact]
    public void Given_a_connector_above_what_the_site_can_give_then_it_is_rejected()
    {
        var row = ActiveRow with { GridConnection = "60 kW", Margin = "20 kW" };

        var outcome = AssetTrackTranslator.Translate([row]).Rows.Single();

        Describe(outcome).ShouldBe("Rejected by EffectiveCapacity");
    }

    [Fact]
    public void Given_the_same_export_twice_then_the_translation_is_the_same()
    {
        var again = AssetTrackTranslator.Translate(Export.Rows());

        again.Rows.ShouldBe(Import.Rows);
        again.Sites.ShouldBe(Import.Sites);
    }

    private static string Describe(AssetRowTranslation outcome) => outcome switch
    {
        Translated => "Translated",
        Skipped skipped => $"Skipped by {skipped.Rule}",
        Rejected rejected => $"Rejected by {rejected.Rule}",
        null => "nothing",
    };

    private static ConnectorConfiguration Connector(
        string chargePoint, int number, PriorityClass priority) =>
        ConnectorConfiguration.Create(
            new ConnectorId(new ChargePointId(chargePoint), number),
            new Power(6m),
            new Power(50m),
            priority);
}
