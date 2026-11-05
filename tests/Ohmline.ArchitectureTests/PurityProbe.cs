using System.Diagnostics;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// Runs <c>build/DomainPurity.targets</c> against a throwaway project in a temporary folder, the
/// way the real build does: just before <c>CoreCompile</c>. Nothing is restored or compiled, so a
/// probe takes about a second.
/// </summary>
internal static class PurityProbe
{
    public static async Task<(int ExitCode, string Output)> BuildAsync(
        string projectName, string references)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var folder = Directory.CreateTempSubdirectory("ohmline-purity-");
        try
        {
            var targets = Path.Combine(Repository.Root.FullName, "build", "DomainPurity.targets");
            var project = Path.Combine(folder.FullName, projectName + ".proj");
            await File.WriteAllTextAsync(project, $"""
                <Project>
                  <ItemGroup>
                    {references}
                  </ItemGroup>
                  <Import Project="{targets}" />
                  <Target Name="CoreCompile" />
                </Project>
                """, cancellation);

            var msbuild = new ProcessStartInfo(
                DotnetHost(),
                ["msbuild", project, "-t:CoreCompile", "-nologo", "-noAutoResponse", "-nr:false"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(msbuild)!;
            var output = process.StandardOutput.ReadToEndAsync(cancellation);
            var errors = process.StandardError.ReadToEndAsync(cancellation);
            await process.WaitForExitAsync(cancellation);

            return (process.ExitCode, await output + await errors);
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
