using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Domain.Sites;

/// <summary>
/// What Site Energy knows about one connector: the power it can draw, from its minimum (below
/// which charging stops) to its maximum, and its priority class. The properties have no init
/// accessor, so <c>with</c> cannot skip the check that the minimum is not above the maximum.
/// </summary>
public sealed record ConnectorConfiguration
{
    private ConnectorConfiguration(
        ConnectorId connectorId, Power minPower, Power maxPower, PriorityClass priority)
    {
        ConnectorId = connectorId;
        MinPower = minPower;
        MaxPower = maxPower;
        Priority = priority;
    }

    public ConnectorId ConnectorId { get; }

    public Power MinPower { get; }

    public Power MaxPower { get; }

    public PriorityClass Priority { get; }

    public static Result<ConnectorConfiguration, ValidationError> TryCreate(
        ConnectorId connectorId, Power minPower, Power maxPower, PriorityClass priority)
    {
        if (maxPower == Power.Zero)
        {
            return Result.Fail<ValidationError>(
                new OutOfRange("Maximum power", maxPower.Kilowatts, "more than 0 kW"));
        }

        return minPower <= maxPower
            ? Result.Ok(new ConnectorConfiguration(connectorId, minPower, maxPower, priority))
            : Result.Fail<ValidationError>(
                new OutOfRange("Minimum power", minPower.Kilowatts, $"at most {maxPower}"));
    }

    /// <summary>For values known to be valid. An invalid value is a bug, so it throws.</summary>
    public static ConnectorConfiguration Create(
        ConnectorId connectorId, Power minPower, Power maxPower, PriorityClass priority) =>
        TryCreate(connectorId, minPower, maxPower, priority)
            .Match(valid => valid, error => throw new ArgumentException(error.Message));
}
