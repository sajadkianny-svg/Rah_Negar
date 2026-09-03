using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Live;

public abstract class LivePilotObserver<TObservation> :
    IControlledPilotOperationalWorkflowObserver
    where TObservation : IControlledPilotBoundaryObservation
{
    private readonly IControlledPilotFingerprintSpecification<TObservation> _fingerprint;

    protected LivePilotObserver(
        IControlledPilotFingerprintSpecification<TObservation> fingerprint)
    {
        _fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
    }

    public PilotValidationWorkflow Workflow => _fingerprint.Workflow;
    public string FingerprintSpecificationVersion => _fingerprint.Version;
    public bool IsAvailable => true;
    public bool IsReadOnly => true;
    public bool SupportsCancellation => true;
    public bool RequiresReview => false;
    public bool ExposesWriteOperation => false;
    public bool OpensWriteTransaction => false;
    public bool ExecutesMigration => false;
    public bool CreatesSession => false;
    public bool MutatesSettings => false;
    public bool MutatesEvents => false;
    public bool ExecutesEsd => false;
    public bool GeneratesExportArtifact => false;

    public async ValueTask<ControlledPilotOperationalWorkflowResult?> ObserveAsync(
        ControlledPilotOperationalRehearsalContext context,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        LivePilotObservationPair<TObservation> pair =
            await ReadPairAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (pair.Legacy.Boundary != OperationalObservationBoundary.LegacyAuthoritative ||
            pair.Target.Boundary != OperationalObservationBoundary.TargetReadOnly ||
            !pair.Legacy.IsValid || !pair.Target.IsValid)
            return null;

        string legacy = _fingerprint.CreateFingerprint(pair.Legacy);
        cancellationToken.ThrowIfCancellationRequested();
        string target = _fingerprint.CreateFingerprint(pair.Target);
        bool match = StringComparer.Ordinal.Equals(legacy, target);
        return new ControlledPilotOperationalWorkflowResult(
            Workflow,
            match ? OperationalWorkflowComparisonStatus.Match :
                OperationalWorkflowComparisonStatus.Difference,
            FingerprintSpecificationVersion,
            legacy,
            target,
            match ? 0 : 1,
            pair.EvidenceReference,
            observedAtUtc);
    }

    protected abstract ValueTask<LivePilotObservationPair<TObservation>> ReadPairAsync(
        CancellationToken cancellationToken);
}

public sealed class LiveAuthenticationPilotObserver :
    LivePilotObserver<AuthenticationOperationalObservation>,
    IAuthenticationOperationalObserver
{
    private readonly ILiveAuthenticationPilotReadModel _readModel;

    public LiveAuthenticationPilotObserver(ILiveAuthenticationPilotReadModel readModel)
        : base(new AuthenticationFingerprintSpecification()) =>
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));

    protected override ValueTask<LivePilotObservationPair<AuthenticationOperationalObservation>>
        ReadPairAsync(CancellationToken cancellationToken) =>
        _readModel.ReadAsync(cancellationToken);
}

public sealed class LiveReportingPilotObserver :
    LivePilotObserver<ReportingOperationalObservation>,
    IReportingOperationalObserver
{
    private readonly ILiveReportingPilotReadModel _readModel;

    public LiveReportingPilotObserver(ILiveReportingPilotReadModel readModel)
        : base(new ReportingFingerprintSpecification()) =>
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));

    protected override ValueTask<LivePilotObservationPair<ReportingOperationalObservation>>
        ReadPairAsync(CancellationToken cancellationToken) =>
        _readModel.ReadAsync(cancellationToken);
}

public sealed class LiveRuntimeEventPilotObserver :
    LivePilotObserver<RuntimeEventOperationalObservation>,
    IRuntimeEventOperationalObserver
{
    private readonly ILiveRuntimeEventPilotReadModel _readModel;

    public LiveRuntimeEventPilotObserver(ILiveRuntimeEventPilotReadModel readModel)
        : base(new RuntimeEventFingerprintSpecification()) =>
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));

    protected override ValueTask<LivePilotObservationPair<RuntimeEventOperationalObservation>>
        ReadPairAsync(CancellationToken cancellationToken) =>
        _readModel.ReadAsync(cancellationToken);
}

public sealed class LiveProtectedSettingsPilotObserver :
    LivePilotObserver<ProtectedSettingsOperationalObservation>,
    IProtectedSettingsOperationalObserver
{
    private readonly ILiveProtectedSettingsPilotReadModel _readModel;

    public LiveProtectedSettingsPilotObserver(ILiveProtectedSettingsPilotReadModel readModel)
        : base(new ProtectedSettingsFingerprintSpecification()) =>
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));

    protected override ValueTask<LivePilotObservationPair<ProtectedSettingsOperationalObservation>>
        ReadPairAsync(CancellationToken cancellationToken) =>
        _readModel.ReadAsync(cancellationToken);
}

public sealed class LiveExportPilotObserver :
    LivePilotObserver<ExportOperationalObservation>,
    IExportOperationalObserver
{
    private readonly ILiveExportPilotReadModel _readModel;

    public LiveExportPilotObserver(ILiveExportPilotReadModel readModel)
        : base(new ExportFingerprintSpecification()) =>
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));

    protected override ValueTask<LivePilotObservationPair<ExportOperationalObservation>>
        ReadPairAsync(CancellationToken cancellationToken) =>
        _readModel.ReadAsync(cancellationToken);
}
