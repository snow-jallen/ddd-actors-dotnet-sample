namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// What a journey answers whoever sent it an input, once its step is stored (section 19.3):
/// accepted at the version the step leaves it at; accepted at the version it started at, for a
/// start it has recorded already; or refused, with a stable code from
/// <see cref="JourneyRefusals"/>.
/// </summary>
public union JourneyAnswer(AcceptedNow, AcceptedAsBefore, Refused);

/// <summary>Accepted: the journey is at the version its step left it at.</summary>
public sealed record AcceptedNow;

/// <summary>
/// The same start again: accepted, as it was the first time, at the version the start was
/// recorded at, with nothing new recorded.
/// </summary>
public sealed record AcceptedAsBefore;

/// <summary>Refused, nothing recorded: a stable code for programs, a sentence for people.</summary>
public sealed record Refused(string Code, string Message);
