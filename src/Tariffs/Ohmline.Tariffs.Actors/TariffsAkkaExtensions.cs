using Akka.Cluster.Hosting;
using Akka.Hosting;
using Ohmline.ActorShell.Clustering;
using Ohmline.ActorShell.Sharding;
using Ohmline.Tariffs.Actors.Book;
using Ohmline.Tariffs.Actors.Serialization;
using Ohmline.Tariffs.Contracts.Book;

namespace Ohmline.Tariffs.Actors;

/// <summary>Everything Tariffs adds to an ActorSystem, in one call.</summary>
public static class TariffsAkkaExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Tariffs' serializer, the tariff book as a cluster singleton on the oldest
        /// <c>charging</c> node, and a proxy to it on this node, registered as
        /// <see cref="TariffBookProxy"/>: the proxy finds the book wherever it runs, and holds
        /// messages for it while it moves.
        /// </summary>
        public AkkaConfigurationBuilder AddTariffsContext()
        {
            var options = new ClusterSingletonOptions
            {
                Role = ClusterRoles.Charging,
                // Like an entity's hand-off: the book stops after what it is already writing.
                TerminationMessage = Stop.Instance,
            };
            return akka
                .WithCustomSerializer(
                    TariffsSerializer.Name,
                    TariffsSerializer.BoundTypes,
                    system => new TariffsSerializer(system))
                .WithSingleton<TariffBookActor>(
                    TariffBookActor.Name,
                    (_, _, resolver) => resolver.Props<TariffBookActor>(),
                    options,
                    createProxyToo: false)
                .WithSingletonProxy<TariffBookProxy>(TariffBookActor.Name, options);
        }

        /// <summary>
        /// Tariffs for a node that only asks: the serializer and the proxy to the tariff book,
        /// which runs on the <c>charging</c> nodes. The Device Gateway asks it for the tariff a
        /// session at its chargers starts with (section 16.7).
        /// </summary>
        public AkkaConfigurationBuilder AddTariffsProxy() =>
            akka
                .WithCustomSerializer(
                    TariffsSerializer.Name,
                    TariffsSerializer.BoundTypes,
                    system => new TariffsSerializer(system))
                .WithSingletonProxy<TariffBookProxy>(
                    TariffBookActor.Name,
                    new ClusterSingletonOptions { Role = ClusterRoles.Charging });
    }
}
