namespace Ch10.PersistenceLab.Tests.Counters;

/// <summary>Add one to the counter, <paramref name="Times"/> times over.</summary>
public sealed record Increment(int Times = 1);

/// <summary>The event: the counter went up by one.</summary>
public sealed record Incremented;

/// <summary>The reply once every event of an increment is stored and applied.</summary>
public sealed record Counted(int Total);

public sealed record GetCount;

public sealed record Count(int Value);
