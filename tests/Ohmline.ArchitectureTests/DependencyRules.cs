using System.Reflection;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// The dependency rules of the solution layout, as predicates over the names of the assemblies
/// that a compiled assembly references. A compiled reference is what actually ships: a package
/// that a project file mentions but no code uses never appears here, and the build target
/// (<c>build/DomainPurity.targets</c>) catches that case instead.
/// </summary>
internal static class DependencyRules
{
    private static readonly string[] InfrastructurePrefixes =
        ["Akka", "OpenTelemetry", "Npgsql", "Microsoft.AspNetCore"];

    private static readonly string RuntimeFolder =
        Path.GetDirectoryName(typeof(object).Assembly.Location)!;

    extension(Assembly assembly)
    {
        public IEnumerable<string> ReferencedNames =>
            assembly.GetReferencedAssemblies().Select(reference => reference.Name!);
    }

    /// <summary>Actors, telemetry, a database driver or the web framework.</summary>
    public static bool IsInfrastructure(string assembly) =>
        InfrastructurePrefixes.Any(prefix =>
            assembly.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Part of the base class library that ships with .NET itself.</summary>
    public static bool IsRuntime(string assembly) =>
        File.Exists(Path.Combine(RuntimeFolder, assembly + ".dll"));

    public static bool IsSharedKernel(string assembly) => assembly == "Ohmline.SharedKernel";

    public static bool IsContracts(string assembly) =>
        assembly.StartsWith("Ohmline.", StringComparison.Ordinal)
        && assembly.EndsWith(".Contracts", StringComparison.Ordinal);
}
