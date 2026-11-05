using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Actors.Sessions;

/// <summary>
/// A started charging session, as its snapshot stores it: the terms, which never change, and
/// the phase it is in. A DTO with a manifest of its own, never the state union.
/// </summary>
public sealed record ChargingSessionSnapshot(
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    TariffSnapshot Tariff,
    Money SpendCap,
    SessionPhaseSnapshot Phase);
