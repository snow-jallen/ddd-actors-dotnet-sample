using Ohmline.ServiceDefaults;

namespace Ohmline.Migrator;

/// <summary>
/// <c>dotnet run --project src/Ohmline.Migrator</c>, with <c>ConnectionStrings:ohmline</c>
/// and, for Billing, <c>ConnectionStrings:billing</c>; Aspire passes both. A connection string
/// that is not set is a database this run leaves alone. The exit code says whether every
/// migration succeeded. Not top-level statements: tests reference this project beside the
/// node's and Billing's, and each would bring a public class named <c>Program</c>.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.AddServiceDefaults();
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Ohmline.Migrator");

        var failed = false;
        foreach (var target in MigrationTarget.All)
        {
            var database = builder.Configuration.GetConnectionString(target.ConnectionName);
            if (database is null)
            {
                logger.LogInformation(
                    "ConnectionStrings:{Name} is not set: skipped", target.ConnectionName);
                continue;
            }

            var result = Migrations.Apply(target, database, logger);
            if (!result.Successful)
            {
                logger.LogError(
                    result.Error,
                    "The {Name} database stopped at {Script}",
                    target.ConnectionName,
                    result.ErrorScript?.Name);
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }
}
