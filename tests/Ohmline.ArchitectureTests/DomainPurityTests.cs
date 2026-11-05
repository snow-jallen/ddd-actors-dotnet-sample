using static Ohmline.ArchitectureTests.DependencyRules;

namespace Ohmline.ArchitectureTests;

public sealed class DomainPurityTests
{
    public static TheoryData<string> DomainProjects => new(Repository.DomainProjects);

    [Theory]
    [MemberData(nameof(DomainProjects))]
    public void A_domain_assembly_references_no_actors_telemetry_database_or_web(string project)
    {
        var references = Repository.Load(project).ReferencedNames;

        references.Where(IsInfrastructure).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(DomainProjects))]
    public void A_domain_assembly_references_only_kernel_contracts_and_runtime(string project)
    {
        var references = Repository.Load(project).ReferencedNames;

        references
            .Where(name => !IsSharedKernel(name) && !IsContracts(name) && !IsRuntime(name))
            .ShouldBeEmpty();
    }
}
