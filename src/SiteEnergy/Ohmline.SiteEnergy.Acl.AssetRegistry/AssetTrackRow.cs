namespace Ohmline.SiteEnergy.Acl.AssetRegistry;

/// <summary>
/// One line of the AssetTrack export, exactly as written: every field is raw text. Only the
/// translator reads these fields; nothing outside this project ever sees them.
/// </summary>
public sealed record AssetTrackRow(
    int Line,
    string AssetId,
    string SiteCode,
    string SiteName,
    string GridConnection,
    string Margin,
    string TimeZone,
    string ChargerSerial,
    string PlugNumber,
    string MinPower,
    string MaxPower,
    string Priority,
    string Status,
    string LastEdit);
