using Microsoft.Data.Sqlite;

namespace Rah_Negar.Infrastructure.Database;

public sealed record SqliteDatabaseOptions
{
    public required string DataSource { get; init; }
    public SqliteOpenMode Mode { get; init; } = SqliteOpenMode.ReadWriteCreate;
    public SqliteCacheMode Cache { get; init; } = SqliteCacheMode.Default;
    public bool Pooling { get; init; } = true;
    public int DefaultTimeoutSeconds { get; init; } = 10;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataSource);
        if (DefaultTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(DefaultTimeoutSeconds));
    }
}
