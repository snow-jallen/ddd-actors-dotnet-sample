using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace Ohmline.Migrator.Tests;

/// <summary>
/// One PostgreSQL 17 container for the tests in <see cref="PostgresCollection"/>; each test
/// asks for an empty database of its own, as a new deployment starts.
/// </summary>
public sealed class PostgresFixture(IMessageSink sink)
    : ContainerFixture<PostgreSqlBuilder, PostgreSqlContainer>(sink)
{
    protected override PostgreSqlBuilder Configure() => new("postgres:17-alpine");

    /// <summary>The connection string of a database that does not exist yet.</summary>
    public string NewDatabase() =>
        new NpgsqlConnectionStringBuilder(Container.GetConnectionString())
        {
            Database = $"migrate_{Guid.NewGuid():N}",
        }.ConnectionString;

    /// <summary>Runs SQL as the container's superuser, in the given database.</summary>
    public static async Task ExecuteAsync(
        string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<IReadOnlyList<string>> ColumnAsync(
        string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
