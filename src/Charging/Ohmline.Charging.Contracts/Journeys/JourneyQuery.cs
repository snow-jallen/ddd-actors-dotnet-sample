using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.Journeys;

/// <summary>A question for a charging journey: a family of its own, never a command.</summary>
public closed record JourneyQuery(JourneyId JourneyId);

/// <summary>Where is the journey, and why did it stop if it did?</summary>
public sealed record GetJourney(JourneyId JourneyId) : JourneyQuery(JourneyId);
