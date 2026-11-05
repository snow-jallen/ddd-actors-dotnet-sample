using System.Text;

namespace Ohmline.SiteEnergy.Acl.AssetRegistry;

/// <summary>
/// Splits the AssetTrack export into rows of raw text. It reads the CSV and nothing more: it
/// trims nothing and judges nothing, because every judgement belongs to the translator. A file
/// it cannot split is not an AssetTrack export at all, and the import stops with an exception.
/// </summary>
public static class AssetTrackCsvReader
{
    public const string Header =
        "ASSET_ID,SITE_CODE,SITE_NAME,GRID_CONN,MARGIN,TZ,CHARGER_SERIAL,PLUG_NO,MIN_PWR,MAX_PWR,"
        + "PRIO,STATUS,LAST_EDIT";

    public static IEnumerable<AssetTrackRow> Read(TextReader reader)
    {
        var header = reader.ReadLine();
        if (header != Header)
        {
            throw new FormatException($"Line 1 is not the AssetTrack header: '{header}'.");
        }

        var line = 1;
        while (reader.ReadLine() is { } text)
        {
            line++;
            if (text.Length > 0)
            {
                yield return ToRow(line, text);
            }
        }
    }

    private static AssetTrackRow ToRow(int line, string text) => Split(line, text) switch
    {
        [var assetId, var siteCode, var siteName, var grid, var margin, var timeZone,
            var serial, var plug, var min, var max, var priority, var status, var lastEdit] =>
            new AssetTrackRow(
                line, assetId, siteCode, siteName, grid, margin, timeZone,
                serial, plug, min, max, priority, status, lastEdit),
        var fields => throw new FormatException(
            $"Line {line} has {fields.Count} fields; an AssetTrack row has 13."),
    };

    /// <summary>Splits at commas outside double quotes; "" inside quotes is one quote.</summary>
    private static List<string> Split(int line, string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '"' when quoted && i + 1 < text.Length && text[i + 1] == '"':
                    field.Append('"');
                    i++;
                    break;
                case '"':
                    quoted = !quoted;
                    break;
                case ',' when !quoted:
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case var other:
                    field.Append(other);
                    break;
            }
        }

        if (quoted)
        {
            throw new FormatException($"Line {line} ends inside a quoted field.");
        }

        fields.Add(field.ToString());
        return fields;
    }
}
