using static Ohmline.ArchitectureTests.DependencyRules;

namespace Ohmline.ArchitectureTests;

public sealed class ContractsPurityTests
{
    public static TheoryData<string> ContractsProjects => new(Repository.ContractsProjects);

    [Theory]
    [MemberData(nameof(ContractsProjects))]
    public void A_contracts_assembly_references_only_the_shared_kernel_and_runtime(string project)
    {
        var references = Repository.Load(project).ReferencedNames;

        references.Where(name => !IsSharedKernel(name) && !IsRuntime(name)).ShouldBeEmpty();
    }
}
