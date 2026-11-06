using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Ohmline.ActorShell.Clustering;

/// <summary>
/// The version a node gives the cluster as its app version (section 21.2): the release's
/// <c>MAJOR.MINOR.PATCH</c>, which <c>Directory.Build.targets</c> writes into every assembly
/// from MinVer's version as the metadata <see cref="MetadataKey"/>. Sharding places new shards
/// on the nodes with the highest app version and pauses rebalancing while versions are mixed
/// (section 21.7), so every release must report a different one.
/// </summary>
public static class OhmlineAppVersion
{
    public const string MetadataKey = "OhmlineAppVersion";

    /// <summary>The app version the build wrote into the assembly, such as <c>1.4.2</c>.</summary>
    /// <exception cref="InvalidOperationException">
    /// The assembly has none, or one that is not <c>MAJOR.MINOR.PATCH</c>: a pre-release such as
    /// <c>1.4.3-alpha.0.1</c> would stop the actor system from starting, so it never gets that far.
    /// </exception>
    public static string Of(Assembly assembly)
    {
        var version = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .LastOrDefault(metadata => metadata.Key == MetadataKey)?.Value;

        // WRONG: whatever the build wrote goes to Akka.NET unchecked.
        return version
            ?? throw new InvalidOperationException(
                $"{assembly.GetName().Name} has the app version '{version}', not " +
                "MAJOR.MINOR.PATCH. Directory.Build.targets writes it from MinVer's version.");
    }

    /// <summary>Three numbers, and no fourth part, pre-release or build metadata.</summary>
    public static bool IsMajorMinorPatch([NotNullWhen(true)] string? version) =>
        Version.TryParse(version, out var parsed) && parsed is { Build: >= 0, Revision: -1 };
}
