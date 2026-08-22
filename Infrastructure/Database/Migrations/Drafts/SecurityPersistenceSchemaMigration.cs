using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database.Checksums;

namespace Rah_Negar.Infrastructure.Database.Migrations.Drafts;

/// <summary>Inactive Phase 7.7 target schema. It is intentionally not registered at startup.</summary>
public sealed class SecurityPersistenceSchemaMigration : IDatabaseMigration
{
    public const string MigrationId = "phase7.7-security-persistence-atomic-esd-v1";
    public const int FromVersion = 1;
    public const int TargetVersion = 2;

    public const string Sql = """
        CREATE TABLE SecurityShiftProfiles (
            ShiftProfileId TEXT PRIMARY KEY NOT NULL,
            StationId TEXT NOT NULL,
            ShiftNumber INTEGER NOT NULL CHECK (ShiftNumber > 0),
            ShiftName TEXT NOT NULL,
            SupervisorFirstName TEXT NOT NULL,
            SupervisorLastName TEXT NOT NULL,
            PersonnelNo TEXT NOT NULL,
            PersonnelNoNormalized TEXT NOT NULL,
            IsActive INTEGER NOT NULL CHECK (IsActive IN (0, 1)),
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL,
            Revision INTEGER NOT NULL CHECK (Revision > 0),
            CHECK (length(trim(ShiftProfileId)) > 0),
            CHECK (length(trim(StationId)) > 0),
            CHECK (length(trim(PersonnelNoNormalized)) > 0),
            CHECK (CreatedAtUtc <= UpdatedAtUtc),
            UNIQUE (StationId, ShiftNumber)
        );
        CREATE UNIQUE INDEX UX_SecurityShiftProfiles_ActivePersonnel
            ON SecurityShiftProfiles (StationId, PersonnelNoNormalized) WHERE IsActive = 1;
        CREATE INDEX IX_SecurityShiftProfiles_Active ON SecurityShiftProfiles (StationId, IsActive, ShiftNumber);

        CREATE TABLE SecurityShiftProfileCredentials (
            ShiftProfileId TEXT NOT NULL,
            CredentialVersion INTEGER NOT NULL CHECK (CredentialVersion > 0),
            KdfAlgorithm TEXT NOT NULL,
            KdfParameters TEXT NOT NULL,
            Salt BLOB NOT NULL CHECK (length(Salt) > 0),
            PasswordVerifier BLOB NOT NULL CHECK (length(PasswordVerifier) > 0),
            IsCurrent INTEGER NOT NULL CHECK (IsCurrent IN (0, 1)),
            CreatedAtUtc TEXT NOT NULL,
            RetiredAtUtc TEXT NULL,
            PRIMARY KEY (ShiftProfileId, CredentialVersion),
            FOREIGN KEY (ShiftProfileId) REFERENCES SecurityShiftProfiles (ShiftProfileId)
                ON UPDATE RESTRICT ON DELETE RESTRICT,
            CHECK ((IsCurrent = 1 AND RetiredAtUtc IS NULL) OR (IsCurrent = 0 AND RetiredAtUtc IS NOT NULL))
        );
        CREATE UNIQUE INDEX UX_SecurityShiftProfileCredentials_Current
            ON SecurityShiftProfileCredentials (ShiftProfileId) WHERE IsCurrent = 1;

        CREATE TABLE SecurityManagementCredentials (
            SingletonId INTEGER NOT NULL CHECK (SingletonId = 1),
            CredentialVersion INTEGER NOT NULL CHECK (CredentialVersion > 0),
            KdfAlgorithm TEXT NOT NULL,
            KdfParameters TEXT NOT NULL,
            Salt BLOB NOT NULL CHECK (length(Salt) > 0),
            PasswordVerifier BLOB NOT NULL CHECK (length(PasswordVerifier) > 0),
            IsCurrent INTEGER NOT NULL CHECK (IsCurrent IN (0, 1)),
            IsActive INTEGER NOT NULL CHECK (IsActive IN (0, 1)),
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL,
            RetiredAtUtc TEXT NULL,
            PRIMARY KEY (SingletonId, CredentialVersion),
            CHECK (CreatedAtUtc <= UpdatedAtUtc),
            CHECK ((IsCurrent = 1 AND RetiredAtUtc IS NULL) OR (IsCurrent = 0 AND RetiredAtUtc IS NOT NULL))
        );
        CREATE UNIQUE INDEX UX_SecurityManagementCredentials_CurrentSingleton
            ON SecurityManagementCredentials (SingletonId) WHERE IsCurrent = 1;

        CREATE TABLE SecurityDeviceIdentity (
            SingletonId INTEGER PRIMARY KEY CHECK (SingletonId = 1),
            DeviceId TEXT NOT NULL UNIQUE,
            ProvisionedAtUtc TEXT NOT NULL,
            Revision INTEGER NOT NULL CHECK (Revision > 0),
            CHECK (length(trim(DeviceId)) >= 16)
        );

        CREATE TABLE SecurityTrustedVendorPublicKeys (
            KeyId TEXT PRIMARY KEY NOT NULL,
            PublicVerificationMaterial BLOB NOT NULL CHECK (length(PublicVerificationMaterial) > 0),
            Algorithm TEXT NOT NULL CHECK (Algorithm = 'ECDSA-P256-SHA256'),
            ActivatedAtUtc TEXT NOT NULL,
            RetiredAtUtc TEXT NULL,
            CreatedAtUtc TEXT NOT NULL,
            Revision INTEGER NOT NULL CHECK (Revision > 0),
            MaterialSha256 TEXT NOT NULL,
            CHECK (RetiredAtUtc IS NULL OR ActivatedAtUtc < RetiredAtUtc)
        );

        CREATE TABLE SecurityDeploymentSettings (
            SingletonId INTEGER PRIMARY KEY CHECK (SingletonId = 1),
            EsdAdjustmentCanonical TEXT NOT NULL,
            Revision INTEGER NOT NULL CHECK (Revision > 0),
            UpdatedAtUtc TEXT NOT NULL,
            UpdatedByShiftProfileId TEXT NULL,
            CHECK (length(trim(EsdAdjustmentCanonical)) > 0),
            FOREIGN KEY (UpdatedByShiftProfileId) REFERENCES SecurityShiftProfiles (ShiftProfileId)
                ON UPDATE RESTRICT ON DELETE RESTRICT
        );

        CREATE TABLE SecurityConsumedVendorAuthorizations (
            RequestId TEXT PRIMARY KEY NOT NULL,
            CorrelationId TEXT NOT NULL,
            DeviceId TEXT NOT NULL,
            Action TEXT NOT NULL CHECK (Action = 'ChangeEsdAdjustment'),
            ProposedEsdAdjustmentCanonical TEXT NOT NULL,
            KeyId TEXT NOT NULL,
            ConsumedAtUtc TEXT NOT NULL,
            InitiatingShiftProfileId TEXT NOT NULL,
            ExecutionReceiptId TEXT NOT NULL UNIQUE,
            ResultStatus TEXT NOT NULL CHECK (ResultStatus IN ('Consumed', 'Succeeded')),
            FOREIGN KEY (InitiatingShiftProfileId) REFERENCES SecurityShiftProfiles (ShiftProfileId)
                ON UPDATE RESTRICT ON DELETE RESTRICT,
            FOREIGN KEY (KeyId) REFERENCES SecurityTrustedVendorPublicKeys (KeyId)
                ON UPDATE RESTRICT ON DELETE RESTRICT
        );

        CREATE TABLE SecurityProtectedExecutionReceipts (
            ExecutionReceiptId TEXT PRIMARY KEY NOT NULL,
            RequestId TEXT NOT NULL UNIQUE,
            CorrelationId TEXT NOT NULL,
            Action TEXT NOT NULL CHECK (Action = 'ChangeEsdAdjustment'),
            InitiatingShiftProfileId TEXT NOT NULL,
            ProposedEsdAdjustmentCanonical TEXT NOT NULL,
            ExecutedAtUtc TEXT NOT NULL,
            ResultStatus TEXT NOT NULL CHECK (ResultStatus = 'Succeeded'),
            ResultingConfigurationRevision INTEGER NOT NULL CHECK (ResultingConfigurationRevision > 0),
            FOREIGN KEY (RequestId) REFERENCES SecurityConsumedVendorAuthorizations (RequestId)
                ON UPDATE RESTRICT ON DELETE RESTRICT,
            FOREIGN KEY (InitiatingShiftProfileId) REFERENCES SecurityShiftProfiles (ShiftProfileId)
                ON UPDATE RESTRICT ON DELETE RESTRICT
        );

        CREATE TABLE SecurityAuditEntries (
            AuditEntryId TEXT PRIMARY KEY NOT NULL,
            InitiatingShiftProfileId TEXT NOT NULL,
            Action TEXT NOT NULL,
            Scope TEXT NOT NULL,
            AuthorizationType TEXT NOT NULL,
            ResultCategory TEXT NOT NULL,
            TimestampUtc TEXT NOT NULL,
            CorrelationId TEXT NOT NULL,
            RequestId TEXT NULL
        );
        CREATE INDEX IX_SecurityAuditEntries_Correlation ON SecurityAuditEntries (CorrelationId, TimestampUtc);
        CREATE INDEX IX_SecurityAuditEntries_Request ON SecurityAuditEntries (RequestId) WHERE RequestId IS NOT NULL;

        CREATE TABLE SecurityAuditMetadata (
            AuditEntryId TEXT NOT NULL,
            MetadataKey TEXT NOT NULL CHECK (MetadataKey IN
                ('DeviceId','RequestId','ProposedEsdAdjustment','AuthorizationStage','ResultCategory','KeyId','CorrelationId')),
            MetadataValue TEXT NOT NULL CHECK (length(trim(MetadataValue)) > 0),
            PRIMARY KEY (AuditEntryId, MetadataKey),
            FOREIGN KEY (AuditEntryId) REFERENCES SecurityAuditEntries (AuditEntryId)
                ON UPDATE RESTRICT ON DELETE RESTRICT
        );

        CREATE TRIGGER TR_SecurityConsumedVendorAuthorizations_NoUpdate BEFORE UPDATE ON SecurityConsumedVendorAuthorizations
        BEGIN SELECT RAISE(ABORT, 'Consumed vendor authorization evidence is immutable'); END;
        CREATE TRIGGER TR_SecurityConsumedVendorAuthorizations_NoDelete BEFORE DELETE ON SecurityConsumedVendorAuthorizations
        BEGIN SELECT RAISE(ABORT, 'Consumed vendor authorization evidence is immutable'); END;
        CREATE TRIGGER TR_SecurityProtectedExecutionReceipts_NoUpdate BEFORE UPDATE ON SecurityProtectedExecutionReceipts
        BEGIN SELECT RAISE(ABORT, 'Protected execution receipts are immutable'); END;
        CREATE TRIGGER TR_SecurityProtectedExecutionReceipts_NoDelete BEFORE DELETE ON SecurityProtectedExecutionReceipts
        BEGIN SELECT RAISE(ABORT, 'Protected execution receipts are immutable'); END;
        CREATE TRIGGER TR_SecurityAuditEntries_NoUpdate BEFORE UPDATE ON SecurityAuditEntries
        BEGIN SELECT RAISE(ABORT, 'Security audit entries are append-only'); END;
        CREATE TRIGGER TR_SecurityAuditEntries_NoDelete BEFORE DELETE ON SecurityAuditEntries
        BEGIN SELECT RAISE(ABORT, 'Security audit entries are append-only'); END;
        CREATE TRIGGER TR_SecurityAuditMetadata_NoUpdate BEFORE UPDATE ON SecurityAuditMetadata
        BEGIN SELECT RAISE(ABORT, 'Security audit metadata is append-only'); END;
        CREATE TRIGGER TR_SecurityAuditMetadata_NoDelete BEFORE DELETE ON SecurityAuditMetadata
        BEGIN SELECT RAISE(ABORT, 'Security audit metadata is append-only'); END;
        """;

    public SecurityPersistenceSchemaMigration(IChecksumService checksums)
    {
        ArgumentNullException.ThrowIfNull(checksums);
        Metadata = new(MigrationId, FromVersion, TargetVersion,
            "Inactive Phase 7.7 security persistence and atomic ESD target schema", checksums.Compute(Sql));
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
