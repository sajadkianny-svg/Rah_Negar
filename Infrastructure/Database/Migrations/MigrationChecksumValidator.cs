using Rah_Negar.Infrastructure.Database.Checksums;

namespace Rah_Negar.Infrastructure.Database.Migrations;

public sealed class MigrationChecksumValidator
{
    private readonly IChecksumService _checksumService;

    public MigrationChecksumValidator(IChecksumService checksumService)
    {
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
    }

    public void Validate(IDatabaseMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);
        string actual = _checksumService.Compute(migration.ChecksumPayload);
        if (!string.Equals(actual, migration.Metadata.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Migration '{migration.Metadata.MigrationId}' checksum validation failed.");
        }
    }
}
