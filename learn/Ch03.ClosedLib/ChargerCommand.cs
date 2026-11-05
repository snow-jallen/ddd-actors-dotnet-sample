namespace Ch03.ClosedLib;

/// <summary>
/// Commands Ohmline sends to a charge point, in the style of OCPP 1.6J. The base is closed, so
/// only this assembly can add a command, and every switch over it knows all of them.
/// </summary>
public closed record ChargerCommand(string ChargePointId);

public sealed record RemoteStart(string ChargePointId, int ConnectorNumber, string TokenUid)
    : ChargerCommand(ChargePointId);

public sealed record RemoteStop(string ChargePointId, string SessionId)
    : ChargerCommand(ChargePointId);

/// <summary>An intermediate level is closed too, so its leaves stay known as well.</summary>
public closed record ChargingProfileCommand(string ChargePointId, int ConnectorNumber)
    : ChargerCommand(ChargePointId);

public sealed record SetChargingProfile(string ChargePointId, int ConnectorNumber, decimal LimitKw)
    : ChargingProfileCommand(ChargePointId, ConnectorNumber);

public sealed record ClearChargingProfile(string ChargePointId, int ConnectorNumber)
    : ChargingProfileCommand(ChargePointId, ConnectorNumber);
