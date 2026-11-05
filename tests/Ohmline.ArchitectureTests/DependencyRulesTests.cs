using static Ohmline.ArchitectureTests.DependencyRules;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// The purity tests pass trivially while the domain projects are nearly empty. These tests show
/// that the rules would catch a violation, and that every project in the solution is covered.
/// </summary>
public sealed class DependencyRulesTests
{
    [Theory]
    [InlineData("Akka")]
    [InlineData("Akka.Cluster.Sharding")]
    [InlineData("OpenTelemetry.Api")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.AspNetCore.Http.Abstractions")]
    public void An_actor_telemetry_database_or_web_assembly_is_infrastructure(string assembly) =>
        IsInfrastructure(assembly).ShouldBeTrue();

    [Theory]
    [InlineData("System.Runtime")]
    [InlineData("System.Collections.Immutable")]
    [InlineData("Ohmline.SharedKernel")]
    [InlineData("Ohmline.Charging.Contracts")]
    public void The_runtime_and_Ohmline_assemblies_are_not_infrastructure(string assembly) =>
        IsInfrastructure(assembly).ShouldBeFalse();

    [Fact]
    public void A_package_assembly_is_not_part_of_the_runtime()
    {
        var references = typeof(DependencyRulesTests).Assembly.ReferencedNames;

        references.Where(name => !IsRuntime(name)).ShouldContain("Shouldly");
    }

    [Fact]
    public void Every_domain_and_contracts_project_in_the_solution_is_checked()
    {
        var projects = Repository.DomainProjects.Concat(Repository.ContractsProjects).ToList();

        projects.ShouldContain("Ohmline.Charging.Domain");
        projects.ShouldContain("Ohmline.Charging.Contracts");
        projects.ShouldAllBe(project => Repository.Load(project).GetName().Name == project);
    }
}
