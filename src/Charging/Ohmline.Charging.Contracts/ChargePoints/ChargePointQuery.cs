using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.ChargePoints;

/// <summary>
/// A question for a charge point. Not a command: answering it changes nothing and records
/// nothing. A family of its own, so that no decider ever sees a question.
/// </summary>
public closed record ChargePointQuery(ChargePointId ChargePointId);

/// <summary>Ask for the charge point's connectors as it knows them now.</summary>
public sealed record GetChargePointStatus(ChargePointId ChargePointId)
    : ChargePointQuery(ChargePointId);
