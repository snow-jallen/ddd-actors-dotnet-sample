using Ohmline.Charging.Contracts.Journeys;

namespace Ohmline.Charging.Api.Responses;

/// <summary>
/// A journey as the edge shows it: the step it is in (one of the constants of
/// <see cref="JourneyView"/>), its session, and, once abandoned, why.
/// </summary>
public sealed record JourneyResponse(
    Guid JourneyId,
    long Version,
    string Step,
    Guid? SessionId,
    string? AbandonReason,
    string? Detail)
{
    public static JourneyResponse From(JourneyView view) => new(
        view.JourneyId.Value,
        view.Version,
        view.Step,
        view.SessionId?.Value,
        view.AbandonReason,
        view.Detail);
}
