using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Foundation.Application.Database.Readiness;

public enum DatabaseTargetFailure
{
    None,
    PathRequired,
    FileNotFound,
    NotAFile,
    InvalidSqliteHeader,
    OpenFailed
}

public sealed record DatabaseTargetDescriptor(
    string ExplicitPath,
    long FileSizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    DateTimeOffset InspectedAtUtc,
    string IdentityFingerprint);

public sealed record DatabaseTargetInspectionResult(
    bool IsValid,
    DatabaseTargetDescriptor? Target,
    DatabaseTargetFailure Failure,
    string ResultCategory);

public interface IExplicitDatabaseTargetInspector
{
    Task<DatabaseTargetInspectionResult> InspectAsync(
        string explicitDatabasePath,
        CancellationToken cancellationToken = default);
}

public enum IntegrityCheckStrategy
{
    QuickCheck,
    FullIntegrityCheck
}

public sealed record SqliteSchemaObject(
    string Type,
    string Name,
    string TableName,
    string DefinitionSha256);

public sealed record InspectedMigrationEntry(
    string MigrationId,
    int FromVersion,
    int ToVersion,
    string Checksum,
    DateTimeOffset? AppliedAtUtc);

public sealed record MigrationLedgerInspection(
    bool VersionTableExists,
    bool HistoryTableExists,
    bool SchemaMatches,
    int? CurrentVersion,
    IReadOnlyList<InspectedMigrationEntry> Entries,
    string? FailureCategory);

public enum InspectedEsdValueState
{
    Absent,
    Valid,
    Invalid,
    MultipleRows,
    RequiredColumnMissing
}

public sealed record EsdValueInspection(
    InspectedEsdValueState State,
    string? CanonicalValue,
    int RowCount);

public sealed record FinalizedEvidenceInspection(
    int SnapshotCount,
    IReadOnlyDictionary<string, string> SnapshotHashes,
    int FinalizedLockCount,
    IReadOnlyDictionary<string, string> LockHashes);

public sealed record DatabasePreflightResult(
    bool Succeeded,
    DatabaseTargetInspectionResult TargetInspection,
    bool HeaderValid,
    bool IntegrityPassed,
    IReadOnlyList<string> IntegrityMessages,
    IReadOnlyList<string> ForeignKeyViolations,
    int SchemaVersion,
    int UserVersion,
    MigrationLedgerInspection MigrationLedger,
    IReadOnlyList<SqliteSchemaObject> SchemaObjects,
    IReadOnlyDictionary<string, long> RowCounts,
    IReadOnlyList<string> LegacyTables,
    IReadOnlyList<string> TargetTables,
    EsdValueInspection LegacyEsd,
    EsdValueInspection TargetEsd,
    FinalizedEvidenceInspection FinalizedEvidence,
    string JournalMode,
    bool ReadOnlyConnectionEnforced,
    bool SourceFileMarkedReadOnly,
    IReadOnlyList<string> Errors);

public interface IReadOnlyDatabasePreflightAnalyzer
{
    Task<DatabasePreflightResult> AnalyzeAsync(
        string explicitDatabasePath,
        IntegrityCheckStrategy integrityStrategy,
        CancellationToken cancellationToken = default);
}

public enum MigrationHistoryClassification
{
    CleanLegacyBaseline,
    CleanUnifiedTarget,
    HistoricalDraftRecognized,
    AdoptionRequired,
    LedgerSchemaMismatch,
    UnknownMigrationHistory,
    ChecksumMismatch,
    CorruptMigrationHistory,
    UnsupportedNewerVersion,
    UnsafeToMigrate
}

public sealed record MigrationHistoryClassificationResult(
    MigrationHistoryClassification Classification,
    int SupportedTargetVersion,
    IReadOnlyList<string> Reasons)
{
    public bool IsMigrationChainSupported => Classification is
        MigrationHistoryClassification.CleanLegacyBaseline or
        MigrationHistoryClassification.CleanUnifiedTarget;
}

public sealed record SupportedMigrationDefinition(
    string MigrationId,
    int FromVersion,
    int ToVersion,
    string Checksum);

public enum HistoricalDraftAdoptionAction
{
    NoAdoptionNeeded,
    BaselineUnifiedChain,
    ValidateExistingSecuritySchema,
    ValidateExistingEventSchema,
    ValidateExistingReportingSchema,
    RequireManualAssessment,
    RejectAutomaticAdoption
}

public sealed record HistoricalDraftAdoptionPlan(
    IReadOnlyList<HistoricalDraftAdoptionAction> Actions,
    bool ManualReviewRequired,
    bool AutomaticAdoptionRejected,
    IReadOnlyList<string> Reasons);

public sealed record DatabaseStructuralFingerprint(
    string Sha256,
    IReadOnlyList<SqliteSchemaObject> SchemaObjects,
    IReadOnlyDictionary<string, long> RowCounts,
    IReadOnlyDictionary<string, string> RepresentativeDataHashes,
    IReadOnlyDictionary<string, string> FinalizedSnapshotHashes,
    IReadOnlyDictionary<string, string> ReportLockHashes,
    string MigrationLedgerHash,
    string? LegacyEsdCanonicalValue,
    string? TargetEsdCanonicalValue);

public interface IDatabaseStructuralFingerprintService
{
    Task<DatabaseStructuralFingerprint> CaptureAsync(
        string explicitDatabasePath,
        CancellationToken cancellationToken = default);
}

public sealed record PreservationVerificationResult(
    bool Passed,
    bool LegacySchemaPreserved,
    bool LegacyRowCountsPreserved,
    bool RepresentativeDataPreserved,
    bool FinalizedSnapshotsPreserved,
    bool ReportLocksPreserved,
    bool LegacyEsdPreserved,
    bool TargetEsdPreserved,
    bool MigrationLedgerProgressValid,
    bool NoRbacIntroduced,
    bool NoSupportIdentityIntroduced,
    IReadOnlyList<string> Issues);

public enum BackupOverwritePolicy
{
    Deny,
    Allow
}

public enum DatabaseBackupFailure
{
    None,
    InvalidSource,
    InvalidDestination,
    SameSourceAndDestination,
    DestinationExists,
    SourceChanged,
    BackupFailed,
    IntegrityFailed,
    ChecksumFailed
}

public sealed record DatabaseBackupVerificationResult(
    bool IsVerified,
    string BackupPath,
    DatabaseTargetDescriptor? SourceIdentity,
    DatabaseTargetDescriptor? BackupIdentity,
    string? BackupSha256,
    long BackupSizeBytes,
    DateTimeOffset CreatedAtUtc,
    int SchemaVersion,
    MigrationHistoryClassification MigrationState,
    bool IntegrityPassed,
    DatabaseBackupFailure Failure,
    IReadOnlyList<string> Errors);

public interface IExplicitSqliteBackupService
{
    Task<DatabaseBackupVerificationResult> CreateVerifiedBackupAsync(
        string explicitSourcePath,
        string explicitDestinationPath,
        BackupOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken = default);
}

public enum RestoreValidationFailure
{
    None,
    PathRequired,
    Missing,
    ChecksumMismatch,
    InvalidSqlite,
    IntegrityFailed,
    UnsupportedMigrationState
}

public sealed record RestoreValidationResult(
    bool IsValid,
    DatabaseTargetDescriptor? BackupIdentity,
    string? ActualSha256,
    int SchemaVersion,
    MigrationHistoryClassification MigrationState,
    bool IntegrityPassed,
    RestoreValidationFailure Failure,
    IReadOnlyList<string> Errors);

public interface IRestoreValidationService
{
    Task<RestoreValidationResult> ValidateAsync(
        string explicitBackupPath,
        string expectedSha256,
        CancellationToken cancellationToken = default);
}

public enum MigrationRehearsalFailure
{
    None,
    BackupNotVerified,
    UnsupportedMigrationState,
    WorkspaceFailed,
    MigrationFailed,
    IntegrityFailed,
    PreservationFailed,
    EsdConflict
}

public sealed record MigrationRehearsalResult(
    bool Passed,
    MigrationRehearsalFailure Failure,
    int InitialVersion,
    int FinalVersion,
    IReadOnlyList<string> AppliedMigrationIds,
    bool IdempotentRerun,
    bool OriginalBackupUnchanged,
    PreservationVerificationResult? Preservation,
    EsdReconciliationState EsdReconciliationState,
    EsdAuthorityMode EsdAuthorityMode,
    IReadOnlyList<string> Errors);

public interface IMigrationRehearsalService
{
    Task<MigrationRehearsalResult> RehearseAsync(
        DatabaseBackupVerificationResult verifiedBackup,
        CancellationToken cancellationToken = default);
}

public interface IIsolatedRehearsalWorkspace : IAsyncDisposable
{
    string DatabaseCopyPath { get; }
}

public interface IIsolatedRehearsalWorkspaceFactory
{
    Task<IIsolatedRehearsalWorkspace> CreateAsync(
        string explicitVerifiedBackupPath,
        CancellationToken cancellationToken = default);
}

public interface ISqliteRetryDelayPolicy
{
    TimeSpan GetDelay(int retryNumber);
}

public sealed class FixedSqliteRetryDelayPolicy : ISqliteRetryDelayPolicy
{
    public FixedSqliteRetryDelayPolicy(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
        Delay = delay;
    }

    public TimeSpan Delay { get; }
    public TimeSpan GetDelay(int retryNumber) => retryNumber > 0
        ? Delay
        : throw new ArgumentOutOfRangeException(nameof(retryNumber));
}

public sealed class SqliteLockBusyPolicy
{
    public SqliteLockBusyPolicy(TimeSpan busyTimeout, int maximumRetryCount, ISqliteRetryDelayPolicy retryDelayPolicy)
    {
        if (busyTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(busyTimeout));
        if (maximumRetryCount is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumRetryCount));
        BusyTimeout = busyTimeout;
        MaximumRetryCount = maximumRetryCount;
        RetryDelayPolicy = retryDelayPolicy ?? throw new ArgumentNullException(nameof(retryDelayPolicy));
    }

    public TimeSpan BusyTimeout { get; }
    public int MaximumRetryCount { get; }
    public ISqliteRetryDelayPolicy RetryDelayPolicy { get; }
}

public interface ISqliteBusyRetryExecutor
{
    Task<T> ExecuteAsync<T>(Func<int, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public sealed record SqliteLockReadinessResult(
    bool IsReady,
    TimeSpan BusyTimeout,
    int MaximumRetryCount,
    string ResultCategory);

public enum DiskSpaceReadinessStatus
{
    Ready,
    InsufficientSpace,
    Unknown
}

public sealed record DiskSpaceEstimate(
    long BackupBytes,
    long RehearsalCopyBytes,
    long MigrationGrowthBytes,
    long JournalWalOverheadBytes,
    long MinimumReserveBytes,
    long TotalRequiredBytes);

public sealed record DiskSpaceReadinessResult(
    DiskSpaceReadinessStatus Status,
    long? AvailableBytes,
    DiskSpaceEstimate Estimate,
    string ResultCategory);

public interface IDiskCapacityProvider
{
    Task<long?> GetAvailableBytesAsync(
        string explicitDestinationPath,
        CancellationToken cancellationToken = default);
}

public sealed record DiskSpaceSafetyPolicy(
    decimal MigrationGrowthRatio,
    decimal JournalWalOverheadRatio,
    long MinimumReserveBytes)
{
    public void Validate()
    {
        if (MigrationGrowthRatio < 0m) throw new ArgumentOutOfRangeException(nameof(MigrationGrowthRatio));
        if (JournalWalOverheadRatio < 0m) throw new ArgumentOutOfRangeException(nameof(JournalWalOverheadRatio));
        if (MinimumReserveBytes < 0) throw new ArgumentOutOfRangeException(nameof(MinimumReserveBytes));
    }
}

public interface IDiskSpaceReadinessService
{
    Task<DiskSpaceReadinessResult> EvaluateAsync(
        long sourceDatabaseSizeBytes,
        string explicitDestinationPath,
        DiskSpaceSafetyPolicy policy,
        CancellationToken cancellationToken = default);
}

public enum OperationalRollbackState
{
    BeforeMigration,
    BackupVerified,
    MigrationStarted,
    MigrationCommitted,
    ValidationPassed,
    ValidationFailed
}

public static class OperationalRollbackExpectations
{
    public static string Describe(OperationalRollbackState state) => state switch
    {
        OperationalRollbackState.BeforeMigration => "No migration has started; preserve the selected source and create a verified backup.",
        OperationalRollbackState.BackupVerified => "A verified restore candidate exists; production remains unchanged.",
        OperationalRollbackState.MigrationStarted => "Do not delete or rewrite history; rely on transaction rollback until commit.",
        OperationalRollbackState.MigrationCommitted => "Do not run destructive reversal; validate and use an approved restore/forward-repair decision.",
        OperationalRollbackState.ValidationPassed => "Retain backup and evidence through the approved observation period.",
        OperationalRollbackState.ValidationFailed => "Stop activation, preserve evidence, and follow approved restore or forward-repair procedures.",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}

public enum MaintenanceReadinessStatus
{
    Blocked,
    ReadyForFutureMigrationApproval
}

public sealed record MaintenanceWindowReadinessInput(
    DatabasePreflightResult Preflight,
    MigrationHistoryClassificationResult MigrationClassification,
    DatabaseBackupVerificationResult Backup,
    MigrationRehearsalResult Rehearsal,
    DiskSpaceReadinessResult DiskSpace,
    SqliteLockReadinessResult LockPolicy,
    bool ExplicitFutureAuthorizationAvailable);

public sealed record MaintenanceWindowReadinessResult(
    MaintenanceReadinessStatus Status,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings);

public sealed record ProductionMigrationReadinessAssessment(
    string DatabaseIdentity,
    DatabasePreflightResult Preflight,
    MigrationHistoryClassificationResult MigrationClassification,
    HistoricalDraftAdoptionPlan AdoptionPlan,
    DatabaseBackupVerificationResult Backup,
    MigrationRehearsalResult Rehearsal,
    EsdReconciliationState EsdReconciliationState,
    bool FinalizedSnapshotPreserved,
    DiskSpaceReadinessResult DiskReadiness,
    SqliteLockReadinessResult LockReadiness,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    MaintenanceReadinessStatus FinalReadinessStatus);
