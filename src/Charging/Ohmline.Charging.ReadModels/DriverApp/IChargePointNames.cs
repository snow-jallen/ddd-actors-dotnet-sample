using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.ReadModels.DriverApp;

/// <summary>
/// Where the driver app's read model gets a charge point's display name: from the listing's
/// current view (section 11.7). A listing keeps no events for projections to read, because state
/// sourcing deletes them.
/// </summary>
public interface IChargePointNames
{
    /// <summary>The display name; null if it is not listed, or did not answer.</summary>
    Task<string?> DisplayNameAsync(
        ChargePointId chargePointId, CancellationToken cancellationToken);
}
