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
global using ListingSpec = Ohmline.Testing.DeciderSpec<
    Ohmline.Charging.Domain.Listings.ChargePointListing,
    Ohmline.Charging.Domain.Listings.ListingState,
    Ohmline.Charging.Contracts.Listings.ChargePointListingCommand,
    Ohmline.Charging.Domain.Listings.ChargePointListingEvent,
    Ohmline.Charging.Domain.Listings.ListingError>;

// The model-based property helper, closed over the same deciders (section 9.4).
global using ChargePointChecks = Ohmline.Testing.DeciderProperties<
    Ohmline.Charging.Domain.ChargePoints.ChargePoint,
    Ohmline.Charging.Domain.ChargePoints.ChargePointState,
    Ohmline.Charging.Contracts.ChargePoints.ChargePointCommand,
    Ohmline.Charging.Domain.ChargePoints.ChargePointEvent,
    Ohmline.Charging.Domain.ChargePoints.ChargePointError>;
global using SessionChecks = Ohmline.Testing.DeciderProperties<
    Ohmline.Charging.Domain.Sessions.ChargingSession,
    Ohmline.Charging.Domain.Sessions.SessionState,
    Ohmline.Charging.Contracts.Sessions.SessionCommand,
    Ohmline.Charging.Domain.Sessions.SessionEvent,
    Ohmline.Charging.Domain.Sessions.SessionError>;
