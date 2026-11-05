using Akka.Hosting;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Serialization;

namespace Ohmline.Charging.Actors;

/// <summary>
/// Everything Charging adds to an ActorSystem, in one call. The node composes bounded contexts
/// with one such extension each, so a context's wiring lives next to its actors, and a test can
/// start exactly the same wiring.
/// </summary>
public static class ChargingAkkaExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Charging's serializer, bound to every message family Charging owns, and the charge
        /// point directory, registered as <see cref="ChargePointDirectory"/>. The journal is the
        /// host's choice.
        /// </summary>
        public AkkaConfigurationBuilder AddChargingContext() =>
            akka
                .WithCustomSerializer(
                    ChargingSerializer.Name,
                    ChargingSerializer.BoundTypes,
                    system => new ChargingSerializer(system))
                .WithActors((system, registry, resolver) =>
                {
                    var clock = resolver.GetService<TimeProvider>();
                    registry.Register<ChargePointDirectory>(
                        system.ActorOf(ChargePointDirectory.Props(clock), "charge-points"));
                });
    }
}
