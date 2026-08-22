using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Persistence;

namespace Rah_Negar.Foundation.Application.Reporting.Finalized;

/// <summary>Reads only immutable snapshot and period-lock contracts; it has no operational source dependency.</summary>
public sealed class SnapshotFinalizedReportReader : IFinalizedReportReader
{
    private readonly IReportSnapshotStore _snapshots;
    private readonly IReportPeriodLockStore _locks;
    private readonly IReadOnlySet<string> _supportedSnapshotFormats;
    private readonly IReadOnlySet<string> _supportedIntegrityFormats;

    public SnapshotFinalizedReportReader(IReportSnapshotStore snapshots, IReportPeriodLockStore locks,
        IEnumerable<string> supportedSnapshotFormats, IEnumerable<string> supportedIntegrityFormats)
    {
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _locks = locks ?? throw new ArgumentNullException(nameof(locks));
        _supportedSnapshotFormats = new HashSet<string>(supportedSnapshotFormats ??
            throw new ArgumentNullException(nameof(supportedSnapshotFormats)), StringComparer.Ordinal);
        _supportedIntegrityFormats = new HashSet<string>(supportedIntegrityFormats ??
            throw new ArgumentNullException(nameof(supportedIntegrityFormats)), StringComparer.Ordinal);
        if (_supportedSnapshotFormats.Count == 0 || _supportedIntegrityFormats.Count == 0)
            throw new ArgumentException("At least one snapshot and integrity format must be supported.");
    }

    public async Task<FinalizedReportReadResult> GetBySnapshotIdAsync(string snapshotId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
            return Failure(FinalizedReportReadStatus.NotFound, "report.snapshot.identity.missing",
                "Snapshot identity is required.");
        try
        {
            FinalizedReportSnapshot? snapshot = await _snapshots.GetByIdAsync(snapshotId, cancellationToken)
                .ConfigureAwait(false);
            return snapshot is null
                ? Failure(FinalizedReportReadStatus.NotFound, "report.snapshot.not-found", "Snapshot was not found.")
                : ValidateSupported(snapshot);
        }
        catch (NotSupportedException)
        {
            return Failure(FinalizedReportReadStatus.IntegrityUnsupported,
                "report.snapshot.format.unsupported", "Snapshot schema or integrity format is unsupported.");
        }
        catch (InvalidDataException)
        {
            return Failure(FinalizedReportReadStatus.IntegrityInvalid,
                "report.snapshot.integrity.invalid", "Snapshot integrity validation failed.");
        }
        catch (ArgumentException)
        {
            return Failure(FinalizedReportReadStatus.IntegrityInvalid,
                "report.snapshot.structure.invalid", "Snapshot domain structure is invalid.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Failure(FinalizedReportReadStatus.InfrastructureFailed,
                "report.snapshot.read.failure", "Snapshot could not be read safely.");
        }
    }

    public async Task<FinalizedReportReadResult> GetEffectiveAsync(FinalizedReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        try
        {
            ReportPeriodLock? periodLock = await _locks.ReadAsync(query.StationId,
                query.PeriodStartMinute, query.PeriodEndMinute, query.PeriodKind, cancellationToken)
                .ConfigureAwait(false);
            if (periodLock is null || periodLock.State == ReportPeriodLockState.Open)
                return Failure(FinalizedReportReadStatus.NotFinalized, "report.period.not-finalized",
                    "The requested period has no finalized target snapshot.");
            if (string.IsNullOrWhiteSpace(periodLock.EffectiveSnapshotId))
                return Failure(FinalizedReportReadStatus.LockSnapshotMismatch,
                    "report.lock.snapshot.missing", "The finalized lock has no effective snapshot identity.");

            FinalizedReportReadResult loaded = await GetBySnapshotIdAsync(periodLock.EffectiveSnapshotId,
                cancellationToken).ConfigureAwait(false);
            if (loaded.Status == FinalizedReportReadStatus.NotFound)
                return Failure(FinalizedReportReadStatus.LockSnapshotMismatch,
                    "report.lock.snapshot.not-found", "The effective snapshot referenced by the lock is missing.");
            if (!loaded.IsSuccess) return loaded;

            FinalizedReportSnapshot snapshot = loaded.Snapshot!;
            if (!Matches(query, periodLock, snapshot))
                return Failure(FinalizedReportReadStatus.LockSnapshotMismatch,
                    "report.lock.snapshot.identity-mismatch",
                    "Period lock and snapshot identities do not match.");
            return loaded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Failure(FinalizedReportReadStatus.InfrastructureFailed,
                "report.finalized.read.failure", "Finalized report could not be read safely.");
        }
    }

    private FinalizedReportReadResult ValidateSupported(FinalizedReportSnapshot snapshot)
    {
        if (!_supportedSnapshotFormats.Contains(snapshot.Versions.SnapshotFormatVersion) ||
            !_supportedIntegrityFormats.Contains(snapshot.Checksum.IntegrityFormatVersion))
            return Failure(FinalizedReportReadStatus.IntegrityUnsupported,
                "report.snapshot.version.unsupported", "Snapshot or integrity version is unsupported.");
        if (snapshot.Checksum.State != SnapshotChecksumState.Calculated ||
            snapshot.Versions.ValidateFor(snapshot.Identity.UnitIds).Count != 0)
            return Failure(FinalizedReportReadStatus.IntegrityInvalid,
                "report.snapshot.evidence.invalid", "Snapshot checksum or version evidence is invalid.");
        return FinalizedReportReadResult.Found(snapshot);
    }

    private static bool Matches(FinalizedReportQuery query, ReportPeriodLock periodLock,
        FinalizedReportSnapshot snapshot) =>
        StringComparer.Ordinal.Equals(periodLock.EffectiveSnapshotId, snapshot.Identity.SnapshotId) &&
        StringComparer.Ordinal.Equals(query.StationId, snapshot.Identity.StationId) &&
        query.PeriodStartMinute == snapshot.Identity.PeriodStartMinute &&
        query.PeriodEndMinute == snapshot.Identity.PeriodEndMinute &&
        StringComparer.Ordinal.Equals(query.PeriodKind, snapshot.Identity.PeriodKind.ToString()) &&
        StringComparer.Ordinal.Equals(periodLock.StationId, snapshot.Identity.StationId) &&
        periodLock.PeriodStartMinute == snapshot.Identity.PeriodStartMinute &&
        periodLock.PeriodEndMinute == snapshot.Identity.PeriodEndMinute &&
        StringComparer.Ordinal.Equals(periodLock.PeriodKind, snapshot.Identity.PeriodKind.ToString());

    private static FinalizedReportReadResult Failure(FinalizedReportReadStatus status,
        string code, string message) => FinalizedReportReadResult.Failure(status, code, message);
}
