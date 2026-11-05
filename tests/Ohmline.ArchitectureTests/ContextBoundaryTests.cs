using System.Xml.Linq;
using static Ohmline.ArchitectureTests.DependencyRules;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// The context map, as project references: a project inside one bounded context's folder
/// (<c>src/Charging/</c>) may reach another context only through that context's Contracts
/// project, never its Domain, Actors, ReadModels or Api. Hosts (web projects) compose contexts
/// and are exempt; projects outside a context folder (the shared kernel, the actor shell) are
/// shared by design.
/// </summary>
public sealed class ContextBoundaryTests
{
    public static TheoryData<string> ContextProjects => new(
        Repository.ProjectFiles.Where(file => ContextOf(file) is not null && !IsHost(file)));

    [Theory]
    [MemberData(nameof(ContextProjects))]
    public void A_context_reaches_other_contexts_only_through_their_contracts(string file)
    {
        var context = ContextOf(file);

        var crossings = ProjectReferences(file)
            .Where(reference => ContextOf(reference) is { } other && other != context)
            .Where(reference => !IsContracts(ProjectName(reference)));

        crossings.ShouldBeEmpty();
    }

    /// <summary><c>Charging</c> for <c>src/Charging/Ohmline.Charging.Api/...csproj</c>.</summary>
    private static string? ContextOf(string file) =>
        file.Replace('\\', '/').Split('/') is ["src", var context, _, _] ? context : null;

    private static string ProjectName(string file) => Path.GetFileNameWithoutExtension(file);

    private static bool IsHost(string file) =>
        Load(file).Root!.Attribute("Sdk")?.Value == "Microsoft.NET.Sdk.Web";

    private static IEnumerable<string> ProjectReferences(string file)
    {
        var folder = Path.GetDirectoryName(Path.Combine(Repository.Root.FullName, file))!;

        return Load(file).Descendants("ProjectReference")
            .Select(reference => Path.GetFullPath(reference.Attribute("Include")!.Value, folder))
            .Select(path => Path.GetRelativePath(Repository.Root.FullName, path));
    }

    private static XDocument Load(string file) =>
        XDocument.Load(Path.Combine(Repository.Root.FullName, file));
}
