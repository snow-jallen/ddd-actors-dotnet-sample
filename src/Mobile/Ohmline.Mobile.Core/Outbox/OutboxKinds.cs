namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// What a row of the outbox asks the server to do; with the row's aggregate key, it names the
/// route the command is sent to. Stored in every row, so a name is never changed: an app
/// upgraded with commands still queued must send them (section 19.9).
/// </summary>
public static class OutboxKinds
{
    /// <summary><c>POST /app/v1/journeys</c>; the key is the journey's id.</summary>
    public const string StartCharging = "start-charging";

    /// <summary><c>POST /app/v1/sessions/{key}/stop</c>; the key is the session's id.</summary>
    public const string StopCharging = "stop-charging";

    /// <summary>
    /// <c>POST /app/v1/charge-points/{id}/connectors/{n}/reservations</c>; the key is the
    /// connector's code, <c>OHM-SLC-0042/1</c>.
    /// </summary>
    public const string ReserveConnector = "reserve-connector";
}
