using Akka.Cluster.Hosting;
using Akka.Hosting;
using Ohmline.ActorShell.Sharding;
using Ohmline.DriverAccounts.Actors.Serialization;
using Ohmline.DriverAccounts.Actors.Tokens;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Actors;

/// <summary>Everything Driver Accounts adds to an ActorSystem, in one call.</summary>
public static class DriverAccountsAkkaExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Driver Accounts' serializer, bound to every message family it owns, and the id token
        /// shard region, registered as <see cref="IdTokenRegion"/>.
        /// </summary>
        public AkkaConfigurationBuilder AddDriverAccountsContext() =>
            akka
                .WithCustomSerializer(
                    DriverAccountsSerializer.Name,
                    DriverAccountsSerializer.BoundTypes,
                    system => new DriverAccountsSerializer(system))
                .WithShardRegion<IdTokenRegion>(
                    IdTokenActor.TypeName,
                    (_, _, resolver) => entityId =>
                        resolver.Props<IdTokenActor>(new TokenUid(entityId)),
                    DriverAccountsMessageExtractors.Tokens,
                    EntityShardOptions.Create());
    }
}
