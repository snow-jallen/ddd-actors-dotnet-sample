namespace Ohmline.ActorShell.Tests.Serialization;

/// <summary>
/// A message that gained a field after it was first written (section 20.2): optional, with a
/// default, so the older shape still reads.
/// </summary>
public sealed record Chime(string Text, int? Volume = null);
