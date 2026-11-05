using System.Xml.Linq;
using Ohmline.Charging.Client.Contracts;
using static Ohmline.ArchitectureTests.DependencyRules;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// The published language for client apps (section 19.2) ships inside every app package, so it
/// references nothing but the runtime: no shared kernel, no other project and no package. The
/// server's Shared Kernel is not the phone's.
/// </summary>
public sealed class ClientContractsTests
{
    private const string Project =
        "src/Charging/Ohmline.Charging.Client.Contracts/Ohmline.Charging.Client.Contracts.csproj";

    [Fact]
    public void The_client_contracts_reference_only_the_runtime()
    {
        typeof(ChargingClientJsonContext).Assembly.ReferencedNames
            .Where(name => !IsRuntime(name))
            .ShouldBeEmpty();
    }

    [Fact]
    public void The_client_contracts_project_has_no_project_or_package_reference()
    {
        var project = XDocument.Load(Path.Combine(Repository.Root.FullName, Project));

        project.Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .ShouldBeEmpty();
    }

    [Fact]
    public void The_client_contracts_are_checked_for_trimming_and_AOT()
    {
        var project = XDocument.Load(Path.Combine(Repository.Root.FullName, Project));

        project.Descendants("IsAotCompatible").Single().Value.ShouldBe("true");
    }
}
