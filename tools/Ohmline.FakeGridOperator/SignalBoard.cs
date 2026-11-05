using System.Collections.Immutable;
using System.Xml;

namespace Ohmline.FakeGridOperator;

/// <summary>Northgate's events, every revision kept, in the order they were announced.</summary>
public sealed class SignalBoard
{
    private readonly Lock _gate = new();
    private ImmutableList<DemandResponseEvent> _revisions = [];

    public DemandResponseEvent Announce(AnnounceEvent announce)
    {
        lock (_gate)
        {
            var modification = _revisions.Count(revision => revision.EventId == announce.EventId);
            var revision = new DemandResponseEvent(
                announce.EventId,
                modification,
                _revisions.Count + 1,
                announce.EventStatus,
                new Targets(announce.VenIds),
                [
                    new Signal(
                        "LOAD_DISPATCH",
                        "delta",
                        "kW",
                        [new Interval(
                            announce.Start,
                            XmlConvert.ToString(announce.Duration),
                            -announce.ReductionKw)]),
                ]);
            _revisions = _revisions.Add(revision);
            return revision;
        }
    }

    public SignalsResponse Since(long sequence)
    {
        var revisions = _revisions;
        return new SignalsResponse(
            [.. revisions.Where(revision => revision.Sequence > sequence)],
            revisions.Count);
    }
}
