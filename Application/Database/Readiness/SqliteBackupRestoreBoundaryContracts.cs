using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Foundation.Application.Database.Readiness;

public enum SqliteBoundaryFailure
{
    None,
    AuthorizationRejected,
    InvalidPath,
    InvalidChecksum,
    SourceMissing,
    DestinationMissing,
    ArtifactCollision,
    PreflightFailed,
    BackupValidationFailed,
    SidecarChanged,
    StagingFailed,
    RollbackCopyFailed,
    FailureInjected,
    SwapFailed,
    PostRestoreValidationFailed,
    RecoveryFailed
}

public sealed record SqliteSidecarEvidence(
    string Suffix,
    bool Present,
    long SizeBytes,
    string? Sha256);

public sealed record SqliteBackupReceipt(
    string ReceiptId,
    string CorrelationId,
    string ActionScope,
    string InitiatingShiftProfileId,
    string SourcePath,
    string BackupPath,
    string? SourceSha256,
    string? BackupSha256,
    long BackupSizeBytes,
    string JournalMode,
    IReadOnlyList<SqliteSidecarEvidence> SourceSidecars,
    bool IntegrityPassed,
    bool ForeignKeysPassed,
    bool Succeeded,
    SqliteBoundaryFailure Failure,
    DateTimeOffset CreatedAtUtc);

public sealed record ManagedSqliteBackupResult(
    bool Succeeded,
    SqliteBoundaryFailure Failure,
    SqliteBackupReceipt Receipt,
    DatabaseBackupVerificationResult Verification,
    IReadOnlyList<string> Errors);

public enum SqliteRestoreFailureInjectionPoint
{
    None,
    AfterRollbackCopy,
    AfterStaging,
    AfterPriorLiveMoved,
    AfterSwapBeforeValidation
}

public sealed record SqliteRestoreReceipt(
    string ReceiptId,
    string CorrelationId,
    string ActionScope,
    string InitiatingShiftProfileId,
    string BackupPath,
    string DestinationPath,
    string RollbackCopyPath,
    string? ExpectedBackupSha256,
    string? DestinationBeforeSha256,
    string? RollbackCopySha256,
    string? DestinationAfterSha256,
    IReadOnlyList<SqliteSidecarEvidence> DestinationSidecarsBefore,
    bool PreRestoreValidationPassed,
    bool PostRestoreValidationPassed,
    bool Succeeded,
    SqliteBoundaryFailure Failure,
    DateTimeOffset CreatedAtUtc);

public sealed record ManagedSqliteRestoreResult(
    bool Succeeded,
    SqliteBoundaryFailure Failure,
    SqliteRestoreReceipt Receipt,
    IReadOnlyList<string> Errors);

public static class SqliteProtectedActionBinding
{
    public static string CreateBackupScope(
        string explicitSourcePath,
        string explicitDestinationPath,
        BackupOverwritePolicy overwritePolicy) =>
        HashScope($"backup|{CanonicalPath(explicitSourcePath)}|{CanonicalPath(explicitDestinationPath)}|{overwritePolicy}");

    public static string CreateRestoreScope(
        string explicitBackupPath,
        string expectedSha256,
        string explicitDestinationPath,
        string explicitRollbackCopyPath) =>
        HashScope($"restore|{CanonicalPath(explicitBackupPath)}|{expectedSha256.Trim().ToUpperInvariant()}|" +
                  $"{CanonicalPath(explicitDestinationPath)}|{CanonicalPath(explicitRollbackCopyPath)}");

    private static string CanonicalPath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string HashScope(string value) =>
        "sqlite-boundary:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)));
}

public interface IManagedSqliteBackupRestoreBoundary
{
    Task<ManagedSqliteBackupResult> CreateVerifiedBackupAsync(
        string explicitSourcePath,
        string explicitDestinationPath,
        BackupOverwritePolicy overwritePolicy,
        ManagementAuthorizationProof managementProof,
        int currentManagementCredentialVersion,
        CancellationToken cancellationToken = default);

    Task<ManagedSqliteRestoreResult> RestoreAsync(
        string explicitBackupPath,
        string expectedBackupSha256,
        string explicitDestinationPath,
        string explicitRollbackCopyPath,
        ManagementAuthorizationProof managementProof,
        int currentManagementCredentialVersion,
        SqliteRestoreFailureInjectionPoint failureInjection = SqliteRestoreFailureInjectionPoint.None,
        CancellationToken cancellationToken = default);
}
