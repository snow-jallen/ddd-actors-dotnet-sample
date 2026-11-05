using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.Chargers;

/// <summary>
/// The DistributedPubSub topic of one charge point: <c>charger-commands-OHM-SLC-0042</c>.
/// Whichever gateway node holds the charger's WebSocket subscribes to it, so Charging reaches
/// the charger without knowing where it is connected. Durable: a renamed topic is silence.
/// </summary>
public static class ChargerTopics
{
    public static string Of(ChargePointId chargePointId) => $"charger-commands-{chargePointId}";
}
