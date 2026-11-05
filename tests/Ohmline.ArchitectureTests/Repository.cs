using System.Reflection;
using System.Xml.Linq;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// The sample repo as the architecture tests see it: its root folder, and the production projects
/// that <c>Ohmline.slnx</c> lists. Reading the solution file, rather than a list in this project,
/// means a new context's projects are checked from the day they are added.
/// </summary>
internal static class Repository
{
    public static DirectoryInfo Root { get; } = FindRoot();

    /// <summary>Every project file in the solution, as a path relative to the root.</summary>
    public static IReadOnlyList<string> ProjectFiles { get; } =
        XDocument.Load(Path.Combine(Root.FullName, "Ohmline.slnx"))
            .Descendants("Project")
            .Select(project => project.Attribute("Path")!.Value)
            .ToList();

    public static IReadOnlyList<string> DomainProjects { get; } = ProjectsEndingWith(".Domain");

    public static IReadOnlyList<string> ContractsProjects { get; } =
        ProjectsEndingWith(".Contracts");

    /// <summary>Loads the assembly a project builds; the two share a name.</summary>
    public static Assembly Load(string project)
    {
        try
        {
            return Assembly.Load(project);
        }
        catch (FileNotFoundException notReferenced)
        {
            throw new InvalidOperationException(
                $"{project} is in Ohmline.slnx, but Ohmline.ArchitectureTests does not reference " +
                "it, so its dependencies cannot be checked.",
                notReferenced);
        }
    }

    private static IReadOnlyList<string> ProjectsEndingWith(string suffix) =>
        ProjectFiles
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => name.StartsWith("Ohmline.", StringComparison.Ordinal)
                           && name.EndsWith(suffix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static DirectoryInfo FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory);
             folder is not null;
             folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Ohmline.slnx")))
            {
                return folder;
            }
        }

        throw new InvalidOperationException($"No Ohmline.slnx above {AppContext.BaseDirectory}.");
    }
}
