using Npgsql;
using Ohmline.Charging.ReadModels;
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

    /// <summary>
    /// The container, then the read models' tables. A node's projection daemons create them a
    /// moment after it starts (section 13.6), so a test that queries at once, before any
    /// projection has run, would find no table instead of no row. Chapter 20's migrator creates
    /// them before any node starts.
    /// </summary>
    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await using var readModels = NpgsqlDataSource.Create(ConnectionString);
        await ReadModelSchema.EnsureAsync(readModels, CancellationToken.None);
    }
}
