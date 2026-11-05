using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Support;

namespace Ohmline.Migrator;

/// <summary>
/// Applies a database's scripts with DbUp: each script once, in the order of its name, each in
/// a transaction of its own, recorded in the database's <c>schemaversions</c> table; then the
/// grants, every time. A script that fails stops the migration with everything before it
/// applied and itself rolled back, so it can be fixed and the migrator run again.
/// </summary>
public static class Migrations
{
    private static readonly Assembly Scripts = typeof(Migrations).Assembly;

    /// <summary>Creates the database if it is missing, then brings its schema up to date.</summary>
    public static DatabaseUpgradeResult Apply(
        MigrationTarget target, string connectionString, ILogger logger)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString, new MicrosoftUpgradeLog(logger));
        return Upgrader(target, connectionString)
            .LogTo(logger)
            .Build()
            .PerformUpgrade();
    }

    /// <summary>The scripts this target has not run yet, in the order they would run.</summary>
    public static IReadOnlyList<string> Pending(MigrationTarget target, string connectionString) =>
        [
            .. Upgrader(target, connectionString)
                .LogToNowhere()
                .Build()
                .GetScriptsToExecute()
                .Select(script => script.Name),
        ];

    private static DbUp.Builder.UpgradeEngineBuilder Upgrader(
        MigrationTarget target, string connectionString) =>
        DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Scripts, name => name.StartsWith(ResourcePrefix($"Scripts.{target.Scripts}.")))
            .WithScriptsEmbeddedInAssembly(
                Scripts,
                name => name == ResourcePrefix($"Grants.{target.Grants}"),
                new SqlScriptOptions
                {
                    ScriptType = ScriptType.RunAlways,
                    RunGroupOrder = DbUpDefaults.DefaultRunGroupOrder + 1,
                })
            .WithTransactionPerScript()
            // DbUp reads $name$ as a variable to substitute; PostgreSQL's dollar quoting, such
            // as the $BLOCK$ in Akka.Persistence.Sql's DDL, is not one.
            .WithVariablesDisabled();

    private static string ResourcePrefix(string path) => $"{Scripts.GetName().Name}.{path}";
}
