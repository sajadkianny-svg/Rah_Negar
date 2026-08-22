using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Foundation.Application.Database.Readiness;

public sealed class MigrationHistoryClassifier
{
    private static readonly HashSet<string> HistoricalDraftIds = new(StringComparer.Ordinal)
    {
        "phase7.7-security-persistence-atomic-esd-v1",
        "event-target-schema-v1-draft",
        "report-snapshot-target-schema-v1-isolated"
    };

    private readonly IReadOnlyList<SupportedMigrationDefinition> _chain;
    private readonly IReadOnlyDictionary<string, SupportedMigrationDefinition> _byId;
    private readonly int _targetVersion;

    public MigrationHistoryClassifier(IEnumerable<SupportedMigrationDefinition> chain, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(chain);
        _chain = chain.OrderBy(x => x.FromVersion).ToArray();
        if (_chain.Count == 0 || targetVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetVersion));
        if (_chain.Select(x => x.MigrationId).Distinct(StringComparer.Ordinal).Count() != _chain.Count)
            throw new ArgumentException("Supported migration identifiers must be unique.", nameof(chain));
        _byId = new ReadOnlyDictionary<string, SupportedMigrationDefinition>(
            _chain.ToDictionary(x => x.MigrationId, StringComparer.Ordinal));
        _targetVersion = targetVersion;
    }

    public MigrationHistoryClassificationResult Classify(DatabasePreflightResult preflight)
    {
        ArgumentNullException.ThrowIfNull(preflight);
        var reasons = new List<string>();
        if (!preflight.Succeeded || !preflight.HeaderValid || !preflight.IntegrityPassed ||
            preflight.ForeignKeyViolations.Count > 0)
            return Result(MigrationHistoryClassification.UnsafeToMigrate,
                "Database validity or integrity preflight did not pass.");

        MigrationLedgerInspection ledger = preflight.MigrationLedger;
        if (ledger.VersionTableExists != ledger.HistoryTableExists ||
            ((ledger.VersionTableExists || ledger.HistoryTableExists) && !ledger.SchemaMatches))
            return Result(MigrationHistoryClassification.LedgerSchemaMismatch,
                "Migration ledger tables are missing or do not match the expected read-only shape.");

        if (!ledger.VersionTableExists)
            return preflight.TargetTables.Count == 0
                ? Result(MigrationHistoryClassification.CleanLegacyBaseline,
                    "No framework ledger or target schema objects are present.")
                : Result(MigrationHistoryClassification.AdoptionRequired,
                    "Target schema objects exist without a framework ledger.");

        if (ledger.CurrentVersion is null)
            return Result(MigrationHistoryClassification.CorruptMigrationHistory,
                "The ledger has no single readable current version.");
        if (ledger.CurrentVersion > _targetVersion)
            return Result(MigrationHistoryClassification.UnsupportedNewerVersion,
                "The database schema version is newer than this application supports.");

        InspectedMigrationEntry[] entries = ledger.Entries.OrderBy(x => x.FromVersion).ToArray();
        if (entries.Select(x => x.MigrationId).Distinct(StringComparer.Ordinal).Count() != entries.Length ||
            entries.Select(x => x.FromVersion).Distinct().Count() != entries.Length ||
            entries.Select(x => x.ToVersion).Distinct().Count() != entries.Length)
            return Result(MigrationHistoryClassification.CorruptMigrationHistory,
                "Migration history contains duplicate identifiers or transitions.");

        if (entries.Any(x => !_byId.ContainsKey(x.MigrationId)))
            return Result(MigrationHistoryClassification.UnknownMigrationHistory,
                "Migration history contains an identifier outside the approved unified chain.");

        bool recognizedDraft = entries.Any(x => HistoricalDraftIds.Contains(x.MigrationId) &&
            x.FromVersion == 0 && x.ToVersion == 1 &&
            (_byId[x.MigrationId].FromVersion != 0 || _byId[x.MigrationId].ToVersion != 1));
        if (recognizedDraft)
            return Result(MigrationHistoryClassification.HistoricalDraftRecognized,
                "A known pre-unification draft transition is present and requires explicit adoption review.");

        foreach (InspectedMigrationEntry entry in entries)
        {
            SupportedMigrationDefinition expected = _byId[entry.MigrationId];
            if (entry.FromVersion != expected.FromVersion || entry.ToVersion != expected.ToVersion)
                return Result(MigrationHistoryClassification.CorruptMigrationHistory,
                    "A known migration has unexpected transition metadata.");
            if (!StringComparer.OrdinalIgnoreCase.Equals(entry.Checksum, expected.Checksum))
                return Result(MigrationHistoryClassification.ChecksumMismatch,
                    "A recorded migration checksum differs from the approved chain.");
        }

        if (!IsContiguous(entries, ledger.CurrentVersion.Value))
            return Result(MigrationHistoryClassification.CorruptMigrationHistory,
                "Migration history is not contiguous with the recorded current version.");

        if (ledger.CurrentVersion == 0 && entries.Length == 0 && preflight.TargetTables.Count == 0)
            return Result(MigrationHistoryClassification.CleanLegacyBaseline,
                "The framework ledger is an untouched version-zero baseline.");

        if (ledger.CurrentVersion == _targetVersion && entries.Length == _chain.Count &&
            _chain.Select(x => x.MigrationId).SequenceEqual(entries.Select(x => x.MigrationId), StringComparer.Ordinal))
            return Result(MigrationHistoryClassification.CleanUnifiedTarget,
                "The complete approved unified chain is present with matching checksums.");

        return Result(MigrationHistoryClassification.AdoptionRequired,
            "The database contains a supported but incomplete target history requiring reviewed adoption planning.");

        MigrationHistoryClassificationResult Result(MigrationHistoryClassification classification, string reason)
        {
            reasons.Add(reason);
            return new(classification, _targetVersion, reasons.AsReadOnly());
        }
    }

    private static bool IsContiguous(IReadOnlyList<InspectedMigrationEntry> entries, int currentVersion)
    {
        if (entries.Count == 0) return currentVersion == 0;
        if (entries[0].FromVersion != 0 || entries[^1].ToVersion != currentVersion) return false;
        for (int index = 1; index < entries.Count; index++)
            if (entries[index].FromVersion != entries[index - 1].ToVersion) return false;
        return true;
    }
}

public sealed class HistoricalDraftAdoptionPlanner
{
    public HistoricalDraftAdoptionPlan Plan(
        DatabasePreflightResult preflight,
        MigrationHistoryClassificationResult classification)
    {
        ArgumentNullException.ThrowIfNull(preflight);
        ArgumentNullException.ThrowIfNull(classification);
        var actions = new List<HistoricalDraftAdoptionAction>();
        var reasons = new List<string>(classification.Reasons);

        switch (classification.Classification)
        {
            case MigrationHistoryClassification.CleanUnifiedTarget:
                actions.Add(HistoricalDraftAdoptionAction.NoAdoptionNeeded);
                break;
            case MigrationHistoryClassification.CleanLegacyBaseline:
                actions.Add(HistoricalDraftAdoptionAction.BaselineUnifiedChain);
                break;
            case MigrationHistoryClassification.HistoricalDraftRecognized:
                AddIfPresent("SecurityShiftProfiles", HistoricalDraftAdoptionAction.ValidateExistingSecuritySchema);
                AddIfPresent("Events", HistoricalDraftAdoptionAction.ValidateExistingEventSchema);
                AddIfPresent("ReportSnapshots", HistoricalDraftAdoptionAction.ValidateExistingReportingSchema);
                if (actions.Count == 0) actions.Add(HistoricalDraftAdoptionAction.RequireManualAssessment);
                break;
            case MigrationHistoryClassification.AdoptionRequired:
                actions.Add(HistoricalDraftAdoptionAction.RequireManualAssessment);
                break;
            default:
                actions.Add(HistoricalDraftAdoptionAction.RejectAutomaticAdoption);
                break;
        }

        bool rejected = actions.Contains(HistoricalDraftAdoptionAction.RejectAutomaticAdoption);
        bool manual = rejected || classification.Classification is
            MigrationHistoryClassification.HistoricalDraftRecognized or
            MigrationHistoryClassification.AdoptionRequired;
        return new(actions.AsReadOnly(), manual, rejected, reasons.AsReadOnly());

        void AddIfPresent(string table, HistoricalDraftAdoptionAction action)
        {
            if (preflight.TargetTables.Contains(table, StringComparer.Ordinal)) actions.Add(action);
        }
    }
}

public static class DatabasePreservationVerifier
{
    private static readonly string[] RbacTokens = ["Role", "Permission", "Rbac"];
    private static readonly string[] SupportIdentityTokens = ["Support"];

    public static PreservationVerificationResult Compare(
        DatabaseStructuralFingerprint before,
        DatabaseStructuralFingerprint after,
        bool migrationLedgerProgressValid)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var issues = new List<string>();
        Dictionary<(string Type, string Name), SqliteSchemaObject> afterSchema = after.SchemaObjects
            .ToDictionary(x => (x.Type, x.Name));
        bool schema = before.SchemaObjects
            .Where(x => !x.Name.StartsWith("__rahnegar_", StringComparison.Ordinal))
            .All(x => afterSchema.TryGetValue((x.Type, x.Name), out SqliteSchemaObject? actual) && actual == x);
        bool rows = before.RowCounts
            .Where(x => !x.Key.StartsWith("__rahnegar_", StringComparison.Ordinal))
            .All(x => after.RowCounts.TryGetValue(x.Key, out long count) && count == x.Value);
        bool representative = DictionaryEntriesPreserved(before.RepresentativeDataHashes, after.RepresentativeDataHashes);
        bool snapshots = DictionariesEqual(before.FinalizedSnapshotHashes, after.FinalizedSnapshotHashes);
        bool locks = DictionariesEqual(before.ReportLockHashes, after.ReportLockHashes);
        bool legacyEsd = StringComparer.Ordinal.Equals(before.LegacyEsdCanonicalValue, after.LegacyEsdCanonicalValue);
        bool targetEsd = StringComparer.Ordinal.Equals(before.TargetEsdCanonicalValue, after.TargetEsdCanonicalValue);
        bool noRbac = after.SchemaObjects.All(x => !RbacTokens.Any(token =>
            x.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));
        bool noSupport = after.SchemaObjects.All(x => !SupportIdentityTokens.Any(token =>
            x.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));

        Add(!schema, "legacy-schema-changed");
        Add(!rows, "legacy-row-count-changed");
        Add(!representative, "representative-data-changed");
        Add(!snapshots, "finalized-snapshot-changed");
        Add(!locks, "report-lock-changed");
        Add(!legacyEsd, "legacy-esd-changed");
        Add(!targetEsd, "target-esd-changed");
        Add(!migrationLedgerProgressValid, "migration-ledger-progress-invalid");
        Add(!noRbac, "rbac-schema-introduced");
        Add(!noSupport, "support-identity-schema-introduced");
        return new(issues.Count == 0, schema, rows, representative, snapshots, locks,
            legacyEsd, targetEsd, migrationLedgerProgressValid, noRbac, noSupport, issues.AsReadOnly());

        void Add(bool condition, string issue)
        {
            if (condition) issues.Add(issue);
        }
    }

    private static bool DictionariesEqual<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> left,
        IReadOnlyDictionary<TKey, TValue> right) where TKey : notnull =>
        left.Count == right.Count && left.All(x => right.TryGetValue(x.Key, out TValue? value) &&
            EqualityComparer<TValue>.Default.Equals(x.Value, value));

    private static bool DictionaryEntriesPreserved<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> before,
        IReadOnlyDictionary<TKey, TValue> after) where TKey : notnull =>
        before.All(x => after.TryGetValue(x.Key, out TValue? value) &&
            EqualityComparer<TValue>.Default.Equals(x.Value, value));
}

public sealed class DiskSpaceReadinessService : IDiskSpaceReadinessService
{
    private readonly IDiskCapacityProvider _capacity;

    public DiskSpaceReadinessService(IDiskCapacityProvider capacity) =>
        _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));

    public async Task<DiskSpaceReadinessResult> EvaluateAsync(long sourceDatabaseSizeBytes,
        string explicitDestinationPath, DiskSpaceSafetyPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (sourceDatabaseSizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sourceDatabaseSizeBytes));
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitDestinationPath);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        DiskSpaceEstimate estimate;
        try
        {
            long growth = checked((long)decimal.Ceiling(sourceDatabaseSizeBytes * policy.MigrationGrowthRatio));
            long overhead = checked((long)decimal.Ceiling(sourceDatabaseSizeBytes * policy.JournalWalOverheadRatio));
            long total = checked(sourceDatabaseSizeBytes + sourceDatabaseSizeBytes + growth + overhead + policy.MinimumReserveBytes);
            estimate = new(sourceDatabaseSizeBytes, sourceDatabaseSizeBytes, growth, overhead,
                policy.MinimumReserveBytes, total);
        }
        catch (OverflowException)
        {
            estimate = new(sourceDatabaseSizeBytes, sourceDatabaseSizeBytes, 0, 0,
                policy.MinimumReserveBytes, long.MaxValue);
            return new(DiskSpaceReadinessStatus.Unknown, null, estimate, "EstimateOverflow");
        }

        long? available = await _capacity.GetAvailableBytesAsync(explicitDestinationPath, cancellationToken)
            .ConfigureAwait(false);
        if (available is null)
            return new(DiskSpaceReadinessStatus.Unknown, null, estimate, "CapacityUnavailable");
        return available >= estimate.TotalRequiredBytes
            ? new(DiskSpaceReadinessStatus.Ready, available, estimate, "Ready")
            : new(DiskSpaceReadinessStatus.InsufficientSpace, available, estimate, "InsufficientSpace");
    }
}

public static class MaintenanceWindowReadinessEvaluator
{
    public static MaintenanceWindowReadinessResult Evaluate(MaintenanceWindowReadinessInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var blockers = new List<string>();
        var warnings = new List<string>();
        if (!input.Preflight.Succeeded || !input.Preflight.IntegrityPassed ||
            input.Preflight.ForeignKeyViolations.Count > 0) blockers.Add("database-integrity-not-passed");
        if (!input.MigrationClassification.IsMigrationChainSupported) blockers.Add("migration-chain-not-supported");
        if (input.MigrationClassification.Classification == MigrationHistoryClassification.ChecksumMismatch)
            blockers.Add("migration-checksum-mismatch");
        if (!input.Backup.IsVerified || !input.Backup.IntegrityPassed) blockers.Add("verified-backup-required");
        if (!input.Rehearsal.Passed) blockers.Add("migration-rehearsal-not-passed");
        if (input.Rehearsal.EsdReconciliationState is
            EsdReconciliationState.Conflict or
            EsdReconciliationState.TargetAlreadyProvisionedDifferentValue)
            blockers.Add("esd-conflict");
        if (input.DiskSpace.Status != DiskSpaceReadinessStatus.Ready) blockers.Add("disk-space-not-ready");
        if (!input.LockPolicy.IsReady) blockers.Add("sqlite-lock-policy-not-ready");
        if (!input.ExplicitFutureAuthorizationAvailable) blockers.Add("future-authorization-not-available");
        if (!StringComparer.OrdinalIgnoreCase.Equals(input.Preflight.JournalMode, "wal"))
            warnings.Add("source-journal-mode-is-not-wal");
        return new(blockers.Count == 0
                ? MaintenanceReadinessStatus.ReadyForFutureMigrationApproval
                : MaintenanceReadinessStatus.Blocked,
            blockers.AsReadOnly(), warnings.AsReadOnly());
    }
}
