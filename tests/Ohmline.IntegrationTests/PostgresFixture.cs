using Microsoft.Extensions.Logging.Abstractions;
using Ohmline.Migrator;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

[assembly: AssemblyFixture(typeof(Ohmline.IntegrationTests.PostgresFixture))]

namespace Ohmline.IntegrationTests;

/// <summary>
/// One PostgreSQL 17 container for every test in the assembly, started before the first and
/// removed after the last. Tests share it, so each one works on charge points of its own.
/// </summary>
public sealed class PostgresFixture(IMessageSink sink)
    : ContainerFixture<PostgreSqlBuilder, PostgreSqlContainer>(sink)
{
    protected override PostgreSqlBuilder Configure() =>
        new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("ohmline");

    /// <summary>
    /// One node at a time. Until section 20.8, nodes that started at once on an empty database
    /// all tried to create the journal's tables, and PostgreSQL refused all but one; the
    /// migrator now makes them once, before any node starts. The collection stays because every
    /// node also binds pbm's fixed port 9110 on the loopback interface (section 18.8).
    /// </summary>
    public const string OneNodeAtATime = "One node at a time";

    public string ConnectionString => Container.GetConnectionString();

    /// <summary>The container, then its schema, as the AppHost does it (section 20.8).</summary>
    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Migrations.Apply(MigrationTarget.Ohmline, ConnectionString, NullLogger.Instance)
            .Successful.ShouldBeTrue();
    }
}
