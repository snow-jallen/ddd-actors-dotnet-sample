using System.Xml.Linq;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// Only the published contracts, and the shared kernel they depend on, become packages, and
/// each one checks its API against the last published version when it is packed (ADR 0016).
/// </summary>
public sealed class PackagingTests
{
    private static readonly string[] Published =
    [
        "Ohmline.Charging.Client.Contracts",
        "Ohmline.Charging.Contracts",
        "Ohmline.SharedKernel",
        "Ohmline.Tariffs.Contracts",
    ];

    [Fact]
    public void Only_the_published_contracts_and_their_kernel_are_packable()
    {
        var packable = Repository.ProjectFiles
            .Where(project => Property(project, "IsPackable") == "true")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        packable.ShouldBe(Published);
    }

    [Fact]
    public void Every_packable_project_validates_its_package()
    {
        var unvalidated = Repository.ProjectFiles
            .Where(project => Property(project, "IsPackable") == "true")
            .Where(project => Property(project, "EnablePackageValidation") != "true");

        unvalidated.ShouldBeEmpty();
    }

    /// <summary>A property as the project file itself sets it; null when it does not.</summary>
    private static string? Property(string project, string name) =>
        XDocument.Load(Path.Combine(Repository.Root.FullName, project))
            .Descendants(name)
            .LastOrDefault()?.Value;
}
