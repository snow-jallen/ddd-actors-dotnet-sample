using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

[assembly: AssemblyFixture(typeof(Ohmline.Telemetry.Tests.PostgresFixture))]

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// One PostgreSQL 17 container for the assembly's node tests, started before the first that
/// asks for it and removed after the last.
/// </summary>
public sealed class PostgresFixture(IMessageSink sink)
    : ContainerFixture<PostgreSqlBuilder, PostgreSqlContainer>(sink)
{
    protected override PostgreSqlBuilder Configure() =>
        new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("ohmline");

    public string ConnectionString => Container.GetConnectionString();
}
