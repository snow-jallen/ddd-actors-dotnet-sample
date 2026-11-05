namespace Ohmline.SiteEnergy.Acl.AssetRegistry.Tests;

public sealed class AssetTrackCsvReaderTests
{
    [Fact]
    public void Given_the_committed_export_when_read_then_lines_2_to_41_are_rows()
    {
        var rows = Export.Rows();

        rows.Count.ShouldBe(40);
        rows[0].Line.ShouldBe(2);
        rows[^1].Line.ShouldBe(41);
        rows[0].AssetId.ShouldBe("AT-10041");
    }

    [Fact]
    public void Given_quoted_fields_with_commas_then_each_stays_one_field()
    {
        var rows = Export.Rows();

        rows.Single(row => row.Line == 2).SiteName.ShouldBe("Salt Lake Depot, Bays 1-6");
        rows.Single(row => row.Line == 20).MinPower.ShouldBe("3,7 kW");
    }

    [Fact]
    public void Given_fields_with_spaces_then_the_reader_leaves_them_to_the_translator()
    {
        Export.Rows().Single(row => row.Line == 7).ChargerSerial.ShouldBe(" OHM-SLC-0043 ");
    }

    [Fact]
    public void Given_a_doubled_quote_inside_quotes_then_it_is_one_quote()
    {
        var csv = AssetTrackCsvReader.Header + "\n"
            + "AT-1,SLC-DEPOT,\"The \"\"old\"\" depot\",200 kW,10 kW,America/Denver,"
            + "OHM-SLC-0041,1,6 kW,50 kW,FLEET,ACTIVE,2025-02-11\n";

        var row = AssetTrackCsvReader.Read(new StringReader(csv)).Single();

        row.SiteName.ShouldBe("The \"old\" depot");
    }

    [Fact]
    public void Given_a_file_with_another_header_when_read_then_it_is_no_assettrack_export()
    {
        var reading = () => AssetTrackCsvReader.Read(new StringReader("id,site\n1,2\n")).ToList();

        reading.ShouldThrow<FormatException>().Message
            .ShouldBe("Line 1 is not the AssetTrack header: 'id,site'.");
    }

    [Fact]
    public void Given_a_row_with_too_few_fields_when_read_then_it_names_the_line()
    {
        var csv = AssetTrackCsvReader.Header + "\nAT-1,SLC-DEPOT\n";

        var reading = () => AssetTrackCsvReader.Read(new StringReader(csv)).ToList();

        reading.ShouldThrow<FormatException>().Message
            .ShouldBe("Line 2 has 2 fields; an AssetTrack row has 13.");
    }
}
