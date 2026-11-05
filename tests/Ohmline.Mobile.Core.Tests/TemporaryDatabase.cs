using Microsoft.Data.Sqlite;

namespace Ohmline.Mobile.Core.Tests;

/// <summary>
/// A SQLite file of the test's own, as the head's <c>ohmline-driver.db3</c> would be, deleted
/// with its -wal and -shm files.
/// </summary>
internal sealed class TemporaryDatabase : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), $"ohmline-driver-test-{Guid.NewGuid():N}.db3");

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
