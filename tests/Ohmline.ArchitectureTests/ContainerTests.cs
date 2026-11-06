using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// The container images (section 21.5): each deployable names its image under <c>ohmline/</c>,
/// and <c>build/Containers.targets</c> tags it with MinVer's version only and runs it as the
/// non-root user. The targets are checked on a throwaway project with a stand-in for MinVer, so
/// no image is built and no registry is needed.
/// </summary>
public sealed class ContainerTests
{
    public static TheoryData<string, string> Deployables => new()
    {
        { "src/Ohmline.Node/Ohmline.Node.csproj", "ohmline/node" },
        {
            "src/DeviceGateway/Ohmline.DeviceGateway/Ohmline.DeviceGateway.csproj",
            "ohmline/gateway"
        },
        { "src/Billing/Ohmline.Billing/Ohmline.Billing.csproj", "ohmline/billing" },
        { "src/Ohmline.Migrator/Ohmline.Migrator.csproj", "ohmline/migrator" },
    };

    [Theory]
    [MemberData(nameof(Deployables))]
    public void Every_deployable_names_its_image(string project, string repository)
    {
        Property(project, "ContainerRepository").ShouldBe(repository);
    }

    [Fact]
    public void No_project_tags_its_image_by_hand()
    {
        Repository.ProjectFiles
            .Where(project => Property(project, "ContainerImageTag") is not null
                              || Property(project, "ContainerImageTags") is not null)
            .ShouldBeEmpty("Images are tagged from MinVer's version (build/Containers.targets).");
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.0.1-alpha.0.3")]
    public async Task Given_minver_s_version_then_the_image_is_tagged_with_it_and_nothing_else(
        string minVerVersion)
    {
        var image = await ProbeAsync(minVerVersion);

        image.Tags.ShouldBe(minVerVersion);
    }

    [Fact]
    public async Task Given_an_image_then_it_runs_as_the_non_root_user()
    {
        var image = await ProbeAsync("1.0.0");

        image.User.ShouldBe("1654");
    }

    private static string? Property(string project, string name) =>
        XDocument.Load(Path.Combine(Repository.Root.FullName, project))
            .Descendants(name)
            .LastOrDefault()?.Value;

    /// <summary>
    /// Runs <c>build/Containers.targets</c> on a project that names an image, after a stand-in
    /// for MinVer's target, and reads back the tags and the user it sets.
    /// </summary>
    private static async Task<(string Tags, string User)> ProbeAsync(string minVerVersion)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var folder = Directory.CreateTempSubdirectory("ohmline-containers-");
        try
        {
            var targets = Path.Combine(Repository.Root.FullName, "build", "Containers.targets");
            var project = Path.Combine(folder.FullName, "Ohmline.Probe.proj");
            await File.WriteAllTextAsync(project, $"""
                <Project>
                  <PropertyGroup>
                    <ContainerRepository>ohmline/probe</ContainerRepository>
                  </PropertyGroup>
                  <Target Name="MinVer">
                    <PropertyGroup>
                      <MinVerVersion>{minVerVersion}</MinVerVersion>
                    </PropertyGroup>
                  </Target>
                  <Import Project="{targets}" />
                </Project>
                """, cancellation);

            var msbuild = new ProcessStartInfo(
                DotnetHost(),
                [
                    "msbuild", project, "-t:MinVer", "-nologo", "-noAutoResponse", "-nr:false",
                    "-getProperty:ContainerImageTags", "-getProperty:ContainerUser",
                ])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(msbuild)!;
            var output = await process.StandardOutput.ReadToEndAsync(cancellation);
            await process.WaitForExitAsync(cancellation);
            process.ExitCode.ShouldBe(0, output);

            var properties = JsonDocument.Parse(output).RootElement.GetProperty("Properties");
            return (
                properties.GetProperty("ContainerImageTags").GetString()!,
                properties.GetProperty("ContainerUser").GetString()!);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private static string DotnetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
        ?? (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root
            ? Path.Combine(root, "dotnet")
            : "dotnet");
}
