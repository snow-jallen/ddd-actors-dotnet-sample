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
    /// One node at a time: nodes that start at once on an empty database all try to create the
    /// journal's tables (autoInitialize), and PostgreSQL refuses all but one of them. Every test
    /// class here is in this collection, so its tests never run beside another class's. Chapter
    /// 20 creates the schema once, before any node starts.
    /// </summary>
    public const string OneNodeAtATime = "One node at a time";

    public string ConnectionString => Container.GetConnectionString();
}
