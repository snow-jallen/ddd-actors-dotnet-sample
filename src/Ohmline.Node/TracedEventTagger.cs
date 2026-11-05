using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Actors.Persistence;
using Ohmline.SiteEnergy.Actors.Persistence;

namespace Ohmline.Node;

/// <summary>
/// The node's tagger for traced events (section 17.7): an event stored in a trace envelope is
/// tagged by whichever context's tagger knows it, so every projection still finds it.
/// </summary>
public sealed class TracedEventTagger()
    : TraceEnvelopeTagger(new ChargingTagger(), new SiteEnergyTagger());
