using Akka.Actor;
using Akka.Hosting;

namespace Ohmline.Charging.Api.Tests;

/// <summary>A shard region as the endpoints see it, played by a test probe.</summary>
internal sealed class Region<TKey>(IActorRef actor) : IRequiredActor<TKey>
{
    public IActorRef ActorRef => actor;

    public Task<IActorRef> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(actor);
}
