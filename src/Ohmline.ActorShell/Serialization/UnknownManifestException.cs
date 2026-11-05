using System.Runtime.Serialization;

namespace Ohmline.ActorShell.Serialization;

/// <summary>
/// The bytes carry a manifest the serializer does not know: written by a newer node, or by a
/// version of a type that was later removed. During recovery it is a poison event: the actor
/// stops and fails the same way every time it starts (section 11.3), so the manifest is kept
/// here for the log line that tells an operator which one it was.
/// </summary>
public sealed class UnknownManifestException(string serializer, int identifier, string manifest)
    : SerializationException(
        $"{serializer} ({identifier}) does not know the manifest '{manifest}'.")
{
    public string Manifest { get; } = manifest;
}
