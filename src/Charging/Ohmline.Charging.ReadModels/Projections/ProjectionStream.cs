namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// One projection's reading of one tag: the unit that has an offset of its own, and that runs
/// in one place in the cluster. A projection that reads two tags is two streams.
/// </summary>
public sealed record ProjectionStream(string ProjectionId, string Tag)
{
    public override string ToString() => $"{ProjectionId}/{Tag}";
}
