using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>A session's request for power that has not been released, admitted or not.</summary>
public sealed record OpenRequest(ConnectorId ConnectorId, SessionId SessionId, Power Requested);
