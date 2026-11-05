namespace Ohmline.SiteEnergy.Acl.AssetRegistry.Tests;

/// <summary>The committed export, <c>tools/legacy-assets/assettrack-export.csv</c>.</summary>
internal static class Export
{
    private static readonly string Path =
        System.IO.Path.Combine(AppContext.BaseDirectory, "assettrack-export.csv");

    public static List<AssetTrackRow> Rows()
    {
        using var reader = File.OpenText(Path);
        return [.. AssetTrackCsvReader.Read(reader)];
    }
}
