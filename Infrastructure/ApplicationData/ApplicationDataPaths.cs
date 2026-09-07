namespace Rah_Negar.Infrastructure.ApplicationData;

/// <summary>
/// Canonical locations for all mutable application state. The installation directory
/// contains binaries only; operational state belongs under the machine-local data root.
/// </summary>
public sealed class ApplicationDataPaths
{
    public const string ProductDirectoryName = "RahNegar";

    private ApplicationDataPaths(string root)
    {
        RootDirectory = Path.GetFullPath(root);
        DataDirectory = Path.Combine(RootDirectory, "Data");
        DataFilesDirectory = Path.Combine(RootDirectory, "DataFiles");
        BackupsDirectory = Path.Combine(RootDirectory, "Backups");
        LogsDirectory = Path.Combine(RootDirectory, "Logs");
        RecoveryDirectory = Path.Combine(RootDirectory, "Recovery");
    }

    public string RootDirectory { get; }
    public string DataDirectory { get; }
    public string DataFilesDirectory { get; }
    public string BackupsDirectory { get; }
    public string LogsDirectory { get; }
    public string RecoveryDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "db.sys");
    public string AuthorityStatePath => Path.Combine(DataFilesDirectory, "authority-state.json");
    public string AuthorityTransitionPath => Path.Combine(DataFilesDirectory, "authority-transition.json");
    public string AuthorityAuditPath => Path.Combine(DataFilesDirectory, "authority-audit.jsonl");
    public string ActivationAuditPath => Path.Combine(DataFilesDirectory, "activation-audit.jsonl");
    public string MigrationStatePath => Path.Combine(RecoveryDirectory, "data-migration-state.json");
    public string RecoveryRequiredPath => Path.Combine(RecoveryDirectory, "recovery-required.json");
    public string BackupKeyPath => Path.Combine(RecoveryDirectory, "backup.key.dpapi");
    public string MigrationAuditPath => Path.Combine(LogsDirectory, "data-migration.jsonl");

    public static ApplicationDataPaths Default { get; } = CreateDefault();
    public static ApplicationDataPaths ForRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Application data root is required.", nameof(root));
        return new ApplicationDataPaths(root);
    }
    public static ApplicationDataPaths CreateDefault() => ForRoot(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductDirectoryName));

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(DataFilesDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(RecoveryDirectory);
    }
}
