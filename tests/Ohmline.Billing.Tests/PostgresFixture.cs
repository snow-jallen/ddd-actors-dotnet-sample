using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Ohmline.Migrator;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace Ohmline.Billing.Tests;

/// <summary>
/// One PostgreSQL 17 container for the tests in <see cref="PostgresCollection"/>, started only
/// when one of them runs. Each test gets a database of its own, with Billing's tables.
/// </summary>
public sealed class PostgresFixture(IMessageSink sink)
    : ContainerFixture<PostgreSqlBuilder, PostgreSqlContainer>(sink)
{
    protected override PostgreSqlBuilder Configure() => new("postgres:17-alpine");

    /// <summary>
    /// A new database with Billing's schema, as the migrator makes it (section 20.8); its
    /// connection string.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var name = $"billing_{Guid.NewGuid():N}";
        await using (var server = NpgsqlDataSource.Create(Container.GetConnectionString()))
        await using (var create = server.CreateCommand($"CREATE DATABASE {name}"))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var connectionString = new NpgsqlConnectionStringBuilder(Container.GetConnectionString())
        {
            Database = name,
        }.ConnectionString;
        Migrations.Apply(MigrationTarget.Billing, connectionString, NullLogger.Instance)
            .Successful.ShouldBeTrue();
        return connectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
