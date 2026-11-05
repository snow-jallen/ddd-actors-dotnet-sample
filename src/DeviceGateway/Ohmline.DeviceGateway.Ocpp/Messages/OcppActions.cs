namespace Ohmline.DeviceGateway.Ocpp.Messages;

/// <summary>
/// The actions of the subset the gateway speaks, with OCPP 1.6J's names. A charger sends the
/// first seven; Ohmline sends the last three.
/// </summary>
public static class OcppActions
{
    public const string BootNotification = "BootNotification";
    public const string Heartbeat = "Heartbeat";
    public const string StatusNotification = "StatusNotification";
    public const string Authorize = "Authorize";
    public const string StartTransaction = "StartTransaction";
    public const string MeterValues = "MeterValues";
    public const string StopTransaction = "StopTransaction";

    public const string RemoteStartTransaction = "RemoteStartTransaction";
    public const string RemoteStopTransaction = "RemoteStopTransaction";
    public const string SetChargingProfile = "SetChargingProfile";
}
