using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// What the journey's charger port answers when it tells a charge point what to do: told back to
/// the journey's actor, so a closed record (ADR 0003).
/// </summary>
public closed record ChargerCommandResult(ConnectorId ConnectorId);

/// <summary>The charge point will do it: for a start, it waits for the car.</summary>
public sealed record ChargerAccepted(ConnectorId ConnectorId) : ChargerCommandResult(ConnectorId);

/// <summary>The charge point refused, for this reason.</summary>
public sealed record ChargerRejected(ConnectorId ConnectorId, string Reason)
    : ChargerCommandResult(ConnectorId);
