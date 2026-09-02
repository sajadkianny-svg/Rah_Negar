using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Operational;

public enum OperationalWorkflowComparisonStatus
{
    Match,
    Difference,
    Failed
}

public sealed class ControlledPilotOperationalWorkflowResult
{
    internal ControlledPilotOperationalWorkflowResult(
        PilotValidationWorkflow workflow,
        OperationalWorkflowComparisonStatus status,
        string fingerprintSpecificationVersion,
        string legacyFingerprint,
        string targetFingerprint,
        int semanticDifferenceCount,
        string evidenceReference,
        DateTimeOffset observedAtUtc)
    {
        Workflow = workflow;
        Status = status;
        FingerprintSpecificationVersion = OperationalText.SafeIdentifier(
            fingerprintSpecificationVersion, "fingerprint-version-unavailable");
        LegacyFingerprint = FingerprintSafety.SafeSha256(legacyFingerprint);
        TargetFingerprint = FingerprintSafety.SafeSha256(targetFingerprint);
        SemanticDifferenceCount = semanticDifferenceCount;
        EvidenceReference = OperationalText.SafeIdentifier(evidenceReference,
            "workflow-evidence-unavailable");
        ObservedAtUtc = observedAtUtc;
    }

    public PilotValidationWorkflow Workflow { get; }
    public OperationalWorkflowComparisonStatus Status { get; }
    public string FingerprintSpecificationVersion { get; }
    public string LegacyFingerprint { get; }
    public string TargetFingerprint { get; }
    public int SemanticDifferenceCount { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public bool LegacyRemainsAuthoritative => true;
    public bool MutatedProduction => false;
    public bool ContainsRawRows => false;
    public bool ContainsSql => false;
}

public interface IControlledPilotOperationalWorkflowObserver
{
    PilotValidationWorkflow Workflow { get; }
    string FingerprintSpecificationVersion { get; }
    bool IsAvailable { get; }
    bool IsReadOnly { get; }
    bool SupportsCancellation { get; }
    bool RequiresReview { get; }

    ValueTask<ControlledPilotOperationalWorkflowResult?> ObserveAsync(
        ControlledPilotOperationalRehearsalContext context,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}

public interface IAuthenticationOperationalObserver :
    IControlledPilotOperationalWorkflowObserver { }
public interface IReportingOperationalObserver :
    IControlledPilotOperationalWorkflowObserver { }
public interface IRuntimeEventOperationalObserver :
    IControlledPilotOperationalWorkflowObserver { }
public interface IProtectedSettingsOperationalObserver :
    IControlledPilotOperationalWorkflowObserver { }
public interface IExportOperationalObserver :
    IControlledPilotOperationalWorkflowObserver { }

public abstract class DeterministicOperationalWorkflowObserver<TObservation> :
    IControlledPilotOperationalWorkflowObserver
{
    private readonly TObservation _legacy;
    private readonly TObservation _target;
    private readonly IControlledPilotFingerprintSpecification<TObservation> _specification;
    private readonly string _evidenceReference;

    protected DeterministicOperationalWorkflowObserver(
        TObservation legacy,
        TObservation target,
        IControlledPilotFingerprintSpecification<TObservation> specification,
        string evidenceReference,
        bool requiresReview = false)
    {
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _specification = specification ?? throw new ArgumentNullException(nameof(specification));
        if (legacy is IControlledPilotBoundaryObservation legacyBoundary &&
            (legacyBoundary.Boundary != OperationalObservationBoundary.LegacyAuthoritative ||
             !legacyBoundary.IsValid))
            throw new ArgumentException("Legacy observation boundary is required.", nameof(legacy));
        if (target is IControlledPilotBoundaryObservation targetBoundary &&
            (targetBoundary.Boundary != OperationalObservationBoundary.TargetReadOnly ||
             !targetBoundary.IsValid))
            throw new ArgumentException("Target read-only observation boundary is required.", nameof(target));
        _evidenceReference = OperationalText.SafeIdentifier(evidenceReference,
            "workflow-evidence-unavailable");
        RequiresReview = requiresReview;
    }

    public PilotValidationWorkflow Workflow => _specification.Workflow;
    public string FingerprintSpecificationVersion => _specification.Version;
    public bool IsAvailable => OperationalText.IsUsableIdentifier(_evidenceReference);
    public bool IsReadOnly => true;
    public bool SupportsCancellation => true;
    public bool RequiresReview { get; }
    public bool AccessesDatabase => false;
    public bool ExecutesProductionWorkflow => false;

    public ValueTask<ControlledPilotOperationalWorkflowResult?> ObserveAsync(
        ControlledPilotOperationalRehearsalContext context,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        string legacyFingerprint = _specification.CreateFingerprint(_legacy);
        cancellationToken.ThrowIfCancellationRequested();
        string targetFingerprint = _specification.CreateFingerprint(_target);
        bool match = StringComparer.Ordinal.Equals(legacyFingerprint, targetFingerprint);
        return ValueTask.FromResult<ControlledPilotOperationalWorkflowResult?>(new(
            Workflow,
            match ? OperationalWorkflowComparisonStatus.Match :
                OperationalWorkflowComparisonStatus.Difference,
            FingerprintSpecificationVersion,
            legacyFingerprint,
            targetFingerprint,
            match ? 0 : 1,
            _evidenceReference,
            observedAtUtc));
    }
}

public sealed class AuthenticationOperationalObserver :
    DeterministicOperationalWorkflowObserver<AuthenticationOperationalObservation>,
    IAuthenticationOperationalObserver
{
    public AuthenticationOperationalObserver(
        AuthenticationOperationalObservation legacy,
        AuthenticationOperationalObservation target,
        AuthenticationFingerprintSpecification specification,
        string evidenceReference,
        bool requiresReview = false)
        : base(legacy, target, specification, evidenceReference, requiresReview) { }
}

public sealed class ReportingOperationalObserver :
    DeterministicOperationalWorkflowObserver<ReportingOperationalObservation>,
    IReportingOperationalObserver
{
    public ReportingOperationalObserver(
        ReportingOperationalObservation legacy,
        ReportingOperationalObservation target,
        ReportingFingerprintSpecification specification,
        string evidenceReference,
        bool requiresReview = false)
        : base(legacy, target, specification, evidenceReference, requiresReview) { }
}

public sealed class RuntimeEventOperationalObserver :
    DeterministicOperationalWorkflowObserver<RuntimeEventOperationalObservation>,
    IRuntimeEventOperationalObserver
{
    public RuntimeEventOperationalObserver(
        RuntimeEventOperationalObservation legacy,
        RuntimeEventOperationalObservation target,
        RuntimeEventFingerprintSpecification specification,
        string evidenceReference,
        bool requiresReview = false)
        : base(legacy, target, specification, evidenceReference, requiresReview) { }
}

public sealed class ProtectedSettingsOperationalObserver :
    DeterministicOperationalWorkflowObserver<ProtectedSettingsOperationalObservation>,
    IProtectedSettingsOperationalObserver
{
    public ProtectedSettingsOperationalObserver(
        ProtectedSettingsOperationalObservation legacy,
        ProtectedSettingsOperationalObservation target,
        ProtectedSettingsFingerprintSpecification specification,
        string evidenceReference,
        bool requiresReview = false)
        : base(legacy, target, specification, evidenceReference, requiresReview) { }
}

public sealed class ExportOperationalObserver :
    DeterministicOperationalWorkflowObserver<ExportOperationalObservation>,
    IExportOperationalObserver
{
    public ExportOperationalObserver(
        ExportOperationalObservation legacy,
        ExportOperationalObservation target,
        ExportFingerprintSpecification specification,
        string evidenceReference,
        bool requiresReview = false)
        : base(legacy, target, specification, evidenceReference, requiresReview) { }
}

public sealed class TargetRuntimeEventOperationalObservationSource
{
    private readonly RuntimeCalculator _runtimeCalculator;

    public TargetRuntimeEventOperationalObservationSource(RuntimeCalculator runtimeCalculator)
    {
        _runtimeCalculator = runtimeCalculator ?? throw new ArgumentNullException(
            nameof(runtimeCalculator));
    }

    public RuntimeEventOperationalObservation Observe(
        string stationId,
        long periodStartMinute,
        long periodEndMinute,
        IEnumerable<RuntimeCalculationContext> unitContexts)
    {
        ArgumentNullException.ThrowIfNull(unitContexts);
        var units = new List<RuntimeUnitOperationalObservation>();
        foreach (RuntimeCalculationContext context in unitContexts.OrderBy(
                     value => value.EventChain.UnitId, StringComparer.Ordinal))
        {
            if (!StringComparer.Ordinal.Equals(context.EventChain.StationId, stationId) ||
                context.PeriodStartMinute != periodStartMinute ||
                context.PeriodEndMinute != periodEndMinute)
                throw new InvalidOperationException("Runtime fixture identity is inconsistent.");

            RuntimeCalculationResult result = _runtimeCalculator.Calculate(context);
            if (!result.IsSuccess || result.Projection is null)
                throw new InvalidOperationException("Runtime fixture did not produce a projection.");
            RuntimeProjection projection = result.Projection;
            RuntimeEventItemObservation[] events = context.EventChain.Events.Select(item =>
                new RuntimeEventItemObservation(item.SourceEventId, item.EventType,
                    item.EventDateTime, item.SourceOrdinal)).ToArray();
            units.Add(new RuntimeUnitOperationalObservation(
                projection.UnitId,
                events,
                projection.PhysicalRuntimeMinutes,
                projection.EsdAdjustmentMinutes,
                projection.AdjustedRuntimeMinutes,
                projection.RuntimeAfterOhMinutes,
                projection.FinalState,
                projection.ServiceDayCount,
                projection.LongestRunMinutes,
                projection.CumulativeTotalRuntimeMinutes,
                context.BaselineVersion));
        }

        if (units.Count == 0)
            throw new ArgumentException("At least one Runtime Unit is required.",
                nameof(unitContexts));
        return new RuntimeEventOperationalObservation(stationId, periodStartMinute,
            periodEndMinute, units, OperationalObservationBoundary.TargetReadOnly);
    }

    public bool UsesTargetRuntimeDomainService => true;
    public bool MutatesEvents => false;
    public bool AccessesDatabase => false;
}

public static class ExportOperationalObservationFactory
{
    public static ExportOperationalObservation Create(
        string snapshotId,
        string intendedRenderer,
        string deterministicFileName,
        string sourceChecksum,
        string artifactFormat,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative)
    {
        string safeSnapshot = OperationalText.SafeIdentifier(snapshotId,
            "snapshot-unavailable");
        string safeRenderer = OperationalText.SafeIdentifier(intendedRenderer,
            "renderer-unavailable");
        string safeFileName = FingerprintSafety.SafeFileName(deterministicFileName);
        string safeChecksum = FingerprintSafety.SafeSha256(sourceChecksum);
        string safeFormat = OperationalText.SafeIdentifier(artifactFormat,
            "format-unavailable");
        string metadata = string.Join('|', safeSnapshot, safeRenderer, safeFileName,
            safeChecksum, safeFormat);
        string metadataFingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(metadata)));
        return new ExportOperationalObservation(safeSnapshot, safeRenderer, safeFileName,
            safeChecksum, safeFormat, metadataFingerprint, boundary);
    }
}
