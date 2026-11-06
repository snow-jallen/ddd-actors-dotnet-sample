namespace Ohmline.ArchitectureTests;

/// <summary>
/// Every project restores from a committed lock file (section 21.4), so CI restores in locked
/// mode and a package graph never changes without a diff in review. A project added without
/// one would be restored afresh on every machine.
/// </summary>
public sealed class LockFileTests
{
    [Fact]
    public void Every_project_in_the_solution_has_a_lock_file_beside_it()
    {
        var missing = Repository.ProjectFiles
            .Where(project => !File.Exists(LockFileOf(project)));

        missing.ShouldBeEmpty("Restore once and commit the new packages.lock.json.");
    }

    [Fact]
    public void The_driver_app_s_head_has_the_lock_file_that_ci_restores_on_linux()
    {
        File.Exists(LockFileOf("mobile/Ohmline.Mobile/Ohmline.Mobile.csproj")).ShouldBeTrue();
    }

    private static string LockFileOf(string project) =>
        Path.Combine(
            Repository.Root.FullName,
            Path.GetDirectoryName(project)!,
            "packages.lock.json");
}
