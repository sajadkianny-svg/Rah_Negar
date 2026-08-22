using Rah_Negar.Foundation.Application.Reporting.Persistence;

namespace Rah_Negar.Foundation.Application.Reporting.Finalization;

public sealed class ReportFinalizationApplicationService : IReportFinalizationService
{
    private readonly IReportFinalizationAuthorizer _authorizer;
    private readonly IReportFinalizationValidator _validator;
    private readonly IReportSnapshotFactory _snapshotFactory;
    private readonly IAtomicReportFinalizationService _atomicFinalization;

    public ReportFinalizationApplicationService(IReportFinalizationAuthorizer authorizer,
        IReportFinalizationValidator validator, IReportSnapshotFactory snapshotFactory,
        IAtomicReportFinalizationService atomicFinalization)
    {
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _snapshotFactory = snapshotFactory ?? throw new ArgumentNullException(nameof(snapshotFactory));
        _atomicFinalization = atomicFinalization ?? throw new ArgumentNullException(nameof(atomicFinalization));
    }

    public async Task<ReportFinalizationApplicationResult> FinalizeAsync(ReportFinalizationRequest request,
        ReportFinalizationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            ReportFinalizationAuthorizationResult authorization = await _authorizer.AuthorizeAsync(
                request, context, cancellationToken).ConfigureAwait(false);
            if (!authorization.IsAuthorized)
                return new(ReportFinalizationApplicationStatus.AuthorizationRejected, null, null,
                    authorization.Failures.Select(x => new ReportFinalizationApplicationError(x.Code, x.Message)));
            if (!StringComparer.Ordinal.Equals(request.ActorIdentity, context.ActorIdentity))
                return Failure(ReportFinalizationApplicationStatus.AuthorizationRejected,
                    "report.finalization.actor-mismatch", "Authorized context and request actor identities differ.");

            FinalizationValidationResult validation = _validator.Validate(request);
            if (!validation.IsValid) return MapValidation(validation);

            ReportFinalizationResult candidate = _snapshotFactory.Create(request, validation);
            if (!candidate.IsSuccess)
                return Failure(ReportFinalizationApplicationStatus.ValidationRejected,
                    "report.finalization.snapshot-candidate-rejected", "Snapshot candidate creation was rejected.");

            AtomicFinalizationResult atomic = await _atomicFinalization.FinalizeAsync(request,
                context.ExpectedLockRevision, context.ExpectedEffectiveSnapshotId, cancellationToken)
                .ConfigureAwait(false);
            return MapAtomic(atomic);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Failure(ReportFinalizationApplicationStatus.InfrastructureFailed,
                "report.finalization.application-failure", "Report finalization failed safely.");
        }
    }

    private static ReportFinalizationApplicationResult MapValidation(FinalizationValidationResult value)
    {
        ReportFinalizationApplicationStatus status = value.Outcome switch
        {
            ReportFinalizationOutcome.IncompleteRejected => ReportFinalizationApplicationStatus.IncompleteRejected,
            ReportFinalizationOutcome.VersionRejected => ReportFinalizationApplicationStatus.VersionRejected,
            ReportFinalizationOutcome.SourceChangedRejected => ReportFinalizationApplicationStatus.Conflict,
            _ => ReportFinalizationApplicationStatus.ValidationRejected
        };
        return new(status, null, null, value.Issues.Select(x =>
            new ReportFinalizationApplicationError(x.Code, x.Message)));
    }

    private static ReportFinalizationApplicationResult MapAtomic(AtomicFinalizationResult value)
    {
        ReportFinalizationApplicationStatus status = value.Outcome switch
        {
            AtomicFinalizationOutcome.Committed => ReportFinalizationApplicationStatus.Succeeded,
            AtomicFinalizationOutcome.IdempotentReplay => ReportFinalizationApplicationStatus.AlreadyFinalized,
            AtomicFinalizationOutcome.ValidationRejected => ReportFinalizationApplicationStatus.ValidationRejected,
            AtomicFinalizationOutcome.SnapshotConflict or AtomicFinalizationOutcome.LockConflict
                or AtomicFinalizationOutcome.ReceiptConflict => ReportFinalizationApplicationStatus.Conflict,
            _ => ReportFinalizationApplicationStatus.InfrastructureFailed
        };
        return new(status, value.SnapshotId, value.LockRevision,
            value.Errors.Select(x => new ReportFinalizationApplicationError(x, x)));
    }

    private static ReportFinalizationApplicationResult Failure(ReportFinalizationApplicationStatus status,
        string code, string message) => new(status, null, null,
            [new ReportFinalizationApplicationError(code, message)]);
}
