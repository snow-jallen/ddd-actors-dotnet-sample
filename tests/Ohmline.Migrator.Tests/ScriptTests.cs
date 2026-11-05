namespace Ohmline.Migrator.Tests;

/// <summary>The scripts the migrator carries, without a database: names, order, targets.</summary>
public sealed class ScriptTests
{
    private static readonly string[] Embedded =
        typeof(Migrations).Assembly.GetManifestResourceNames();

    [Fact]
    public void The_scripts_are_numbered_once_across_both_databases_and_run_in_that_order()
    {
        Embedded.Where(name => name.Contains(".Scripts.", StringComparison.Ordinal))
            .Select(name => name.Split('.')[^2])
            .Order(StringComparer.Ordinal)
            .ShouldBe(
            [
                "0001_akka_journal",
                "0002_akka_snapshot",
                "0003_tags",
                "0004_read_models",
                "0005_billing",
                "0006_session_history_v2",
            ]);
    }

    [Fact]
    public void Each_database_has_a_folder_of_scripts_and_a_grants_script()
    {
        foreach (var target in MigrationTarget.All)
        {
            Embedded.ShouldContain(name => name.Contains(
                $".Scripts.{target.Scripts}.", StringComparison.Ordinal));
            Embedded.ShouldContain($"Ohmline.Migrator.Grants.{target.Grants}");
        }
    }
}
