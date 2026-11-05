using static Ohmline.ArchitectureTests.DependencyRules;

namespace Ohmline.ArchitectureTests;

/// <summary>
/// The shared kernel is shared by every context, so it may depend on nothing but .NET itself:
/// no packages, no actors and no context (src/Ohmline.SharedKernel/GOVERNANCE.md).
/// </summary>
public sealed class SharedKernelPurityTests
{
    [Fact]
    public void The_shared_kernel_references_only_the_runtime()
    {
        var references = Repository.Load("Ohmline.SharedKernel").ReferencedNames;

        references.Where(name => !IsRuntime(name)).ShouldBeEmpty();
    }
}
