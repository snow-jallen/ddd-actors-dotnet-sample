namespace Ohmline.ActorShell.Tests.Serialization;

/// <summary>
/// The same field added the wrong way: nullable, but without a default, so the constructor
/// requires it and the older shape no longer reads.
/// </summary>
public sealed record Gong(string Text, int? Volume);
