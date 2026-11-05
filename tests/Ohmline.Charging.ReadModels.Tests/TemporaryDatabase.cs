using Microsoft.Data.Sqlite;

namespace Ohmline.Charging.ReadModels.Tests;

/// <summary>A SQLite file of the test's own, deleted with its -wal and -shm files.</summary>
internal sealed class TemporaryDatabase : IDisposable
{
    public TemporaryDatabase() =>
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"ohmline-read-{Guid.NewGuid():N}.db");

    public string Path { get; }

    public string ConnectionString => $"Data Source={Path}";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(Path + suffix);
        }
    }
}
