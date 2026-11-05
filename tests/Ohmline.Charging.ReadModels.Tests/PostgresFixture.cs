using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Ohmline.Migrator;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace Ohmline.Charging.ReadModels.Tests;

/// <summary>
/// One PostgreSQL 17 container for the tests in <see cref="PostgresCollection"/>, started only
/// when one of them runs, so the fast loop needs no container. Each test asks for a database of
/// its own, with the schema the migrator makes (section 20.8), and never sees another test's
/// rows.
/// </summary>
public sealed class PostgresFixture(IMessageSink sink)
    : ContainerFixture<PostgreSqlBuilder, PostgreSqlContainer>(sink)
{
    protected override PostgreSqlBuilder Configure() => new("postgres:17-alpine");

    public async Task<NpgsqlDataSource> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var name = $"test_{Guid.NewGuid():N}";
        await using (var server = NpgsqlDataSource.Create(Container.GetConnectionString()))
        await using (var create = server.CreateCommand($"CREATE DATABASE {name}"))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var connectionString = new NpgsqlConnectionStringBuilder(Container.GetConnectionString())
        {
            Database = name,
        }.ConnectionString;
        Migrations.Apply(MigrationTarget.Ohmline, connectionString, NullLogger.Instance)
            .Successful.ShouldBeTrue();
        return NpgsqlDataSource.Create(connectionString);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
