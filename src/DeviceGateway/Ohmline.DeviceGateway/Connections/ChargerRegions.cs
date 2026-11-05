using Akka.Actor;

namespace Ohmline.DeviceGateway.Connections;

/// <summary>
/// Where a connection actor sends what it hears: the proxies to Charging's charge point and
/// session regions, the proxy to the tariff book, and the DistributedPubSub mediator it
/// subscribes with.
/// </summary>
public sealed record ChargerRegions(
    IActorRef ChargePoints, IActorRef Sessions, IActorRef TariffBook, IActorRef Mediator);
