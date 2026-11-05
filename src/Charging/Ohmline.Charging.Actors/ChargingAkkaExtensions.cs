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
        /// WRONG ON PURPOSE (branch wrong/ch10-s03-default-serializer): no serializer is
        /// registered, so Akka.NET 1.5 falls back to Newtonsoft.Json for every Charging message.
        /// </summary>
        public AkkaConfigurationBuilder AddChargingContext() => akka;
    }
}
