using static Ohmline.ArchitectureTests.DependencyRules;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// A contracts assembly is published language: the shared kernel, the runtime, and the
/// published language of an upstream context (Charging's StartSession carries Tariffs' tariff
/// snapshot, section 8.2). Never a domain, an actor or a package.
/// </summary>
public sealed class ContractsPurityTests
{
    public static TheoryData<string> ContractsProjects => new(Repository.ContractsProjects);

    [Theory]
    [MemberData(nameof(ContractsProjects))]
    public void A_contracts_assembly_references_only_kernel_contracts_and_runtime(string project)
    {
        var references = Repository.Load(project).ReferencedNames;

        references
            .Where(name => !IsSharedKernel(name) && !IsContracts(name) && !IsRuntime(name))
            .ShouldBeEmpty();
    }
}
