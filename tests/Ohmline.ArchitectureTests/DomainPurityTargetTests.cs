namespace Ohmline.ArchitectureTests;

public sealed class DomainPurityTargetTests
{
    [Fact]
    public async Task A_domain_project_that_references_Akka_fails_to_build()
    {
        var (exitCode, output) = await PurityProbe.BuildAsync(
            "Ohmline.Probe.Domain", """<PackageReference Include="Akka.Cluster.Hosting" />""");

        exitCode.ShouldNotBe(0);
        output.ShouldContain("OHM0001: Ohmline.Probe.Domain references Akka.Cluster.Hosting.");
    }

    [Fact]
    public async Task A_domain_project_that_uses_the_ASP_NET_Core_framework_fails_to_build()
    {
        const string AspNetCore = """<FrameworkReference Include="Microsoft.AspNetCore.App" />""";

        var (exitCode, output) = await PurityProbe.BuildAsync("Ohmline.Probe.Domain", AspNetCore);

        exitCode.ShouldNotBe(0);
        output.ShouldContain("OHM0001: Ohmline.Probe.Domain references Microsoft.AspNetCore.App.");
    }

    [Fact]
    public async Task A_domain_project_without_infrastructure_builds()
    {
        var (exitCode, output) = await PurityProbe.BuildAsync("Ohmline.Probe.Domain", "");

        exitCode.ShouldBe(0, output);
    }

    [Fact]
    public async Task An_actors_project_may_reference_Akka()
    {
        var (exitCode, output) = await PurityProbe.BuildAsync(
            "Ohmline.Probe.Actors", """<PackageReference Include="Akka.Cluster.Hosting" />""");

        exitCode.ShouldBe(0, output);
    }
}
