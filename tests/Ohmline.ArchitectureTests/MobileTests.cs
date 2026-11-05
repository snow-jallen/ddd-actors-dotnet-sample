using System.Xml.Linq;
using Ohmline.Mobile.Core.Api;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// The domain stays on the server (ADR 0014, section 19.4): the driver app's logic reaches the
/// server only through Charging's published language, and no mobile project brings Akka.NET,
/// a server project or an ASP.NET Core server assembly onto the phone. The library must not
/// reference MAUI either, so that it builds and is tested on any machine.
/// </summary>
public sealed class MobileTests
{
    private const string ClientContracts = "Ohmline.Charging.Client.Contracts";

    /// <summary>The ASP.NET Core assemblies a client may use: SignalR's .NET client.</summary>
    private static readonly string[] ClientSide =
        [".Client", ".Client.Core", ".Common", ".Protocols.Json"];

    /// <summary>Every project file under <c>src/Mobile</c> and <c>mobile/</c>.</summary>
    public static TheoryData<string> MobileProjects => new(
        new[] { "src/Mobile", "mobile" }
            .Select(folder => Path.Combine(Repository.Root.FullName, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(
                folder, "*.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(Repository.Root.FullName, path))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("obj"))
            .Order(StringComparer.Ordinal));

    [Fact]
    public void The_app_core_references_no_Ohmline_assembly_but_the_client_contracts()
    {
        References()
            .Where(name => name.StartsWith("Ohmline.", StringComparison.Ordinal))
            .ShouldBe([ClientContracts]);
    }

    [Fact]
    public void The_app_core_references_no_actor_MAUI_or_server_assembly()
    {
        References()
            .Where(name => name.StartsWith("Akka", StringComparison.Ordinal)
                           || name.StartsWith("Microsoft.Maui", StringComparison.Ordinal)
                           || IsAspNetCoreServer(name))
            .ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(MobileProjects))]
    public void A_mobile_project_references_no_Akka_package_and_no_server_project(string project)
    {
        var file = Path.Combine(Repository.Root.FullName, project);
        var document = XDocument.Load(file);

        document.Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? "")
            .Where(package => package.StartsWith("Akka", StringComparison.Ordinal))
            .ShouldBeEmpty();
        document.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")!.Value))
            .Where(name => !name.StartsWith("Ohmline.Mobile", StringComparison.Ordinal)
                           && name != ClientContracts)
            .ShouldBeEmpty();
    }

    private static IEnumerable<string> References() =>
        typeof(ChargingApiClient).Assembly.GetReferencedAssemblies().Select(name => name.Name!);

    private static bool IsAspNetCoreServer(string name) =>
        name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
        && !ClientSide.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal));
}
