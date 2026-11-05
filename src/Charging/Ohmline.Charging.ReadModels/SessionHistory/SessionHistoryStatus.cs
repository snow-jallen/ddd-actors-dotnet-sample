namespace Ohmline.Charging.ReadModels.SessionHistory;

/// <summary>The values of <c>session_history.status</c>, as the query API returns them.</summary>
public static class SessionHistoryStatus
{
    public const string Charging = "charging";

    public const string SuspendedByEv = "suspended-by-ev";

    public const string SuspendedByCharger = "suspended-by-charger";

    public const string Ended = "ended";
}
