using Akka.Hosting;

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
        /// Empty for now. Section 10.3 adds Charging's serializer and section 10.4 the charge
        /// point actor.
        /// </summary>
        public AkkaConfigurationBuilder AddChargingContext() => akka;
    }
}
