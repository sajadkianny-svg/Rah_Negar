using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Tests.Database;

internal sealed class TemporarySqliteDatabase : IAsyncDisposable
{
    private readonly string _directory;

    private TemporarySqliteDatabase(string directory, string path)
    {
        _directory = directory;
        Path = path;
        Factory = new SqliteConnectionFactory(new SqliteDatabaseOptions
        {
            DataSource = path,
            Pooling = false
        });
    }

    public string Path { get; }
    public SqliteConnectionFactory Factory { get; }

    public static TemporarySqliteDatabase Create()
    {
        string directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "RahNegar.Tests", Guid.NewGuid().ToString("N"));
        string path = System.IO.Path.Combine(directory, "fixture.sqlite");
        string productionSuffix = System.IO.Path.Combine("Data", "db.sys");
        if (path.EndsWith(productionSuffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A test database cannot use the production database path.");
        return new TemporarySqliteDatabase(directory, path);
    }

    public ValueTask DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}
