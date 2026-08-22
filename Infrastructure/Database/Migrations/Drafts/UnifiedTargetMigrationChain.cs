using Rah_Negar.Infrastructure.Database.Checksums;

namespace Rah_Negar.Infrastructure.Database.Migrations.Drafts;

/// <summary>Explicit inactive chain factory. No runtime discovery or startup registration exists.</summary>
public static class UnifiedTargetMigrationChain
{
    public const int FinalVersion = 4;

    public static IReadOnlyList<IDatabaseMigration> Create(IChecksumService checksums)
    {
        ArgumentNullException.ThrowIfNull(checksums);
        return [
            new DatabaseFoundationSchemaMigration(checksums),
            new SecurityPersistenceSchemaMigration(checksums),
            new EventTargetSchemaMigration(checksums),
            new ReportSnapshotSchemaMigration(checksums)
        ];
    }
}
