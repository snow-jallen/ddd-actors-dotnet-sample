using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Contracts.Journeys;

/// <summary>
/// Everything a charging journey can be told by the edge. Every command names its journey,
/// which is how the shell routes it, and carries the id its sender gave it.
/// </summary>
public closed record JourneyCommand(CommandId CommandId, JourneyId JourneyId);

/// <summary>
/// A driver asks to charge on a connector with a token. The edge has looked up the connector's
/// site and the most power it can draw, and chosen how much to hold on the driver's card.
/// </summary>
public sealed record StartCharging(
    CommandId CommandId,
    JourneyId JourneyId,
    TokenUid TokenUid,
    ConnectorId ConnectorId,
    SiteId SiteId,
    Power MaxPower,
    Money Hold)
    : JourneyCommand(CommandId, JourneyId);
