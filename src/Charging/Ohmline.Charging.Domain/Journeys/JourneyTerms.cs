using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// What a journey was started with, which every later state keeps: who charges where, with how
/// much power at most, and how much is held on the driver's card. Its session is named after it.
/// <paramref name="StartedBy"/> is the command that started it, so that the same command sent
/// again is recognised, however long after (section 19.3).
/// </summary>
public sealed record JourneyTerms(
    JourneyId JourneyId,
    SessionId SessionId,
    TokenUid TokenUid,
    ConnectorId ConnectorId,
    SiteId SiteId,
    Power MaxPower,
    Money Hold,
    CommandId StartedBy);
