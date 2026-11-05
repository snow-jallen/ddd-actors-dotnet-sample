using Ohmline.SharedKernel.Identity;

namespace Ohmline.ActorShell.Deciders;

/// <summary>
/// A command the aggregate accepted, and its version after it: what a duplicate is answered
/// with. Stored in every aggregate's snapshot, so the snapshot DTOs carry a list of these.
/// </summary>
public sealed record RecentCommand(CommandId CommandId, long Version);
