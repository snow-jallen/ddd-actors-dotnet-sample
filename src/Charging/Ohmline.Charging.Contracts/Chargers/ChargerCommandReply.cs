using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.Chargers;

/// <summary>The gateway's answer for its charger: a closed record, as it is told back.</summary>
public closed record ChargerCommandReply(ConnectorId ConnectorId);

/// <summary>The charger will do it.</summary>
public sealed record ChargerCommandAccepted(ConnectorId ConnectorId)
    : ChargerCommandReply(ConnectorId);

/// <summary>The charger refused, or never answered: why, for people and logs.</summary>
public sealed record ChargerCommandRejected(ConnectorId ConnectorId, string Reason)
    : ChargerCommandReply(ConnectorId);
