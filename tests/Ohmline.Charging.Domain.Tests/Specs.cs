// The Given-When-Then harness, closed over each of Charging's deciders once for every test.
global using ChargePointSpec = Ohmline.Testing.DeciderSpec<
    Ohmline.Charging.Domain.ChargePoints.ChargePoint,
    Ohmline.Charging.Domain.ChargePoints.ChargePointState,
    Ohmline.Charging.Contracts.ChargePoints.ChargePointCommand,
    Ohmline.Charging.Domain.ChargePoints.ChargePointEvent,
    Ohmline.Charging.Domain.ChargePoints.ChargePointError>;
global using SessionSpec = Ohmline.Testing.DeciderSpec<
    Ohmline.Charging.Domain.Sessions.ChargingSession,
    Ohmline.Charging.Domain.Sessions.SessionState,
    Ohmline.Charging.Contracts.Sessions.SessionCommand,
    Ohmline.Charging.Domain.Sessions.SessionEvent,
    Ohmline.Charging.Domain.Sessions.SessionError>;
