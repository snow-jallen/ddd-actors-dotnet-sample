namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// Where the query endpoints read the session history (section 20.9): the first version's
/// table, or the second's, which also knows each session's site. Both are kept up to date by
/// their projections at once; this only says which one answers.
/// </summary>
public sealed record SessionHistorySource(int Version, string Table, string SiteColumn)
{
    public static SessionHistorySource Version1 { get; } =
        new(1, "read_models.session_history", "NULL::text");

    public static SessionHistorySource Version2 { get; } =
        new(2, "read_models.session_history_v2", "site_id");

    public static SessionHistorySource Of(int version) => version switch
    {
        1 => Version1,
        2 => Version2,
        _ => throw new ArgumentOutOfRangeException(
            nameof(version), version, "The session history has versions 1 and 2."),
    };
}
