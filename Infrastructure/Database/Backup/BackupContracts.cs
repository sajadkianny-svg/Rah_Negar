namespace Rah_Negar.Infrastructure.Database.Backup;

public enum BackupStatus
{
    Started,
    Completed,
    Failed,
    Corrupt,
    Missing,
    Restored,
    Superseded
}

public sealed record BackupMetadata(
    Guid BackupId,
    string DatabaseIdentity,
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    Guid? CreatedByShiftProfileId,
    string FilePath,
    long SizeBytes,
    string Checksum,
    BackupStatus Status,
    string BackupType);

public sealed record BackupVerificationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    string? ActualChecksum = null);

public interface IBackupVerificationService
{
    Task<BackupVerificationResult> VerifyAsync(
        BackupMetadata metadata,
        CancellationToken cancellationToken = default);
}
