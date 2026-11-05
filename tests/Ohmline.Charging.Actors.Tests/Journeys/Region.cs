using Akka.Actor;
using Akka.Hosting;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>Another context's shard region, as the journey sees it, played by a probe.</summary>
internal sealed class Region<TKey>(IActorRef actor) : IRequiredActor<TKey>
{
    public IActorRef ActorRef => actor;

    public Task<IActorRef> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(actor);
}
