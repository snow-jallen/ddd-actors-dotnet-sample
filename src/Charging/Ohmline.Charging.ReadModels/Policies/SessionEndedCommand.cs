using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.Charging.ReadModels.Policies;

/// <summary>
/// The commands a session's end causes, in two other contexts: a union of their own records,
/// which share no base type. It stays in this process; the case records are what is sent.
/// </summary>
public union SessionEndedCommand(ReleaseAllocation, UnlockFromSession);
