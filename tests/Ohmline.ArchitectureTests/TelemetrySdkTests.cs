using System.Xml.Linq;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// Telemetry is a port (section 17.1): the contexts record through the BCL's ActivitySource
/// and Meter, and only the shared host defaults choose and wire the OpenTelemetry SDK.
/// </summary>
public sealed class TelemetrySdkTests
{
    private const string Sdk = "OpenTelemetry";

    [Fact]
    public void Only_the_service_defaults_reference_an_OpenTelemetry_package()
    {
        var referencing = Repository.ProjectFiles
            .Where(project => project.StartsWith("src/", StringComparison.Ordinal))
            .Where(project => PackagesOf(project).Any(package =>
                package.StartsWith(Sdk, StringComparison.Ordinal)))
            .Select(Path.GetFileNameWithoutExtension);

        referencing.ShouldBe(["Ohmline.ServiceDefaults"]);
    }

    private static IEnumerable<string> PackagesOf(string project) =>
        XDocument.Load(Path.Combine(Repository.Root.FullName, project))
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? "");
}
