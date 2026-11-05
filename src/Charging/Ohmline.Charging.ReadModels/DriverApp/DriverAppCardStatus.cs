namespace Ohmline.Charging.ReadModels.DriverApp;

/// <summary>
/// The values of <c>driver_app_cards.status</c>: the driver app's words, not the session
/// history's. A driver has a car, not an EV.
/// </summary>
public static class DriverAppCardStatus
{
    public const string Charging = "charging";

    public const string PausedByCar = "paused-by-car";

    public const string PausedByCharger = "paused-by-charger";

    public const string Ended = "ended";
}
