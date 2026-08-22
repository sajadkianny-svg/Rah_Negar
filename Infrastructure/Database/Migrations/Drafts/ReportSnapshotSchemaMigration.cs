using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database.Checksums;

namespace Rah_Negar.Infrastructure.Database.Migrations.Drafts;

/// <summary>Unregistered target Reporting migration. Production startup never discovers or runs it.</summary>
public sealed class ReportSnapshotSchemaMigration : IDatabaseMigration
{
    public const string MigrationId = "report-snapshot-target-schema-v1-isolated";
    public const int FromVersion = 3;
    public const int ToVersion = 4;

    public const string Sql = """
        CREATE TABLE ReportSnapshots (
            SnapshotId TEXT COLLATE BINARY PRIMARY KEY NOT NULL CHECK (length(trim(SnapshotId)) > 0),
            ReportId TEXT COLLATE BINARY NOT NULL CHECK (length(trim(ReportId)) > 0),
            StationId TEXT COLLATE BINARY NOT NULL CHECK (length(trim(StationId)) > 0),
            PeriodStartMinute INTEGER NOT NULL,
            PeriodEndMinute INTEGER NOT NULL CHECK (PeriodEndMinute > PeriodStartMinute),
            PeriodKind TEXT COLLATE BINARY NOT NULL,
            SnapshotSequence INTEGER NOT NULL CHECK (SnapshotSequence > 0),
            SupersedesSnapshotId TEXT COLLATE BINARY NULL,
            PayloadSchemaVersion INTEGER NOT NULL CHECK (PayloadSchemaVersion > 0),
            CanonicalJson TEXT NOT NULL CHECK (length(CanonicalJson) > 0),
            ChecksumAlgorithm TEXT COLLATE BINARY NOT NULL,
            IntegrityFormatVersion TEXT COLLATE BINARY NOT NULL,
            ChecksumValue TEXT COLLATE BINARY NOT NULL CHECK (length(ChecksumValue) > 0),
            CanonicalPayloadLength INTEGER NOT NULL CHECK (CanonicalPayloadLength >= 0),
            SourceRevision TEXT COLLATE BINARY NOT NULL,
            FinalizedAt TEXT NOT NULL,
            CONSTRAINT UQ_ReportSnapshots_Lineage UNIQUE
                (StationId, PeriodStartMinute, PeriodEndMinute, PeriodKind, SnapshotSequence),
            CONSTRAINT FK_ReportSnapshots_Supersedes FOREIGN KEY (SupersedesSnapshotId)
                REFERENCES ReportSnapshots (SnapshotId) ON UPDATE RESTRICT ON DELETE RESTRICT,
            CONSTRAINT CK_ReportSnapshots_Lineage CHECK (
                (SnapshotSequence = 1 AND SupersedesSnapshotId IS NULL) OR
                (SnapshotSequence > 1 AND SupersedesSnapshotId IS NOT NULL))
        );

        CREATE TABLE ReportPeriodLocks (
            StationId TEXT COLLATE BINARY NOT NULL,
            PeriodStartMinute INTEGER NOT NULL,
            PeriodEndMinute INTEGER NOT NULL CHECK (PeriodEndMinute > PeriodStartMinute),
            PeriodKind TEXT COLLATE BINARY NOT NULL,
            LockState TEXT COLLATE BINARY NOT NULL CHECK (LockState IN ('Open', 'Finalized')),
            EffectiveSnapshotId TEXT COLLATE BINARY NULL,
            Revision INTEGER NOT NULL CHECK (Revision >= 0),
            FinalizationId TEXT COLLATE BINARY NULL,
            FinalizedAt TEXT NULL,
            ActorIdentity TEXT NULL,
            PRIMARY KEY (StationId, PeriodStartMinute, PeriodEndMinute, PeriodKind),
            CONSTRAINT FK_ReportPeriodLocks_Snapshot FOREIGN KEY (EffectiveSnapshotId)
                REFERENCES ReportSnapshots (SnapshotId) ON UPDATE RESTRICT ON DELETE RESTRICT,
            CONSTRAINT CK_ReportPeriodLocks_State CHECK (
                (LockState = 'Open' AND EffectiveSnapshotId IS NULL) OR
                (LockState = 'Finalized' AND EffectiveSnapshotId IS NOT NULL AND Revision > 0))
        );

        CREATE TABLE ReportFinalizationReceipts (
            FinalizationId TEXT COLLATE BINARY PRIMARY KEY NOT NULL CHECK (length(trim(FinalizationId)) > 0),
            RequestFingerprint TEXT COLLATE BINARY NOT NULL CHECK (length(RequestFingerprint) > 0),
            SnapshotId TEXT COLLATE BINARY NOT NULL UNIQUE,
            StationId TEXT COLLATE BINARY NOT NULL,
            PeriodStartMinute INTEGER NOT NULL,
            PeriodEndMinute INTEGER NOT NULL CHECK (PeriodEndMinute > PeriodStartMinute),
            PeriodKind TEXT COLLATE BINARY NOT NULL,
            LockRevision INTEGER NOT NULL CHECK (LockRevision > 0),
            FinalizedAt TEXT NOT NULL,
            ActorIdentity TEXT NOT NULL,
            CONSTRAINT FK_ReportFinalizationReceipts_Snapshot FOREIGN KEY (SnapshotId)
                REFERENCES ReportSnapshots (SnapshotId) ON UPDATE RESTRICT ON DELETE RESTRICT
        );

        CREATE INDEX IX_ReportSnapshots_Period
            ON ReportSnapshots (StationId, PeriodStartMinute, PeriodEndMinute, PeriodKind, SnapshotSequence);
        CREATE INDEX IX_ReportFinalizationReceipts_Period
            ON ReportFinalizationReceipts (StationId, PeriodStartMinute, PeriodEndMinute, PeriodKind);

        CREATE TRIGGER TR_ReportSnapshots_NoUpdate BEFORE UPDATE ON ReportSnapshots
        BEGIN SELECT RAISE(ABORT, 'ReportSnapshots are immutable'); END;
        CREATE TRIGGER TR_ReportSnapshots_NoDelete BEFORE DELETE ON ReportSnapshots
        BEGIN SELECT RAISE(ABORT, 'ReportSnapshots are immutable'); END;
        CREATE TRIGGER TR_ReportFinalizationReceipts_NoUpdate BEFORE UPDATE ON ReportFinalizationReceipts
        BEGIN SELECT RAISE(ABORT, 'ReportFinalizationReceipts are immutable'); END;
        CREATE TRIGGER TR_ReportFinalizationReceipts_NoDelete BEFORE DELETE ON ReportFinalizationReceipts
        BEGIN SELECT RAISE(ABORT, 'ReportFinalizationReceipts are immutable'); END;
        """;

    public ReportSnapshotSchemaMigration(IChecksumService checksumService)
    {
        ArgumentNullException.ThrowIfNull(checksumService);
        string checksum = checksumService.Compute(Sql);
        Metadata = new(MigrationId, FromVersion, ToVersion,
            "Create isolated immutable report snapshot, period lock, and finalization receipt stores.", checksum);
    }

    public MigrationMetadata Metadata { get; }
    public string ChecksumPayload => Sql;

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
