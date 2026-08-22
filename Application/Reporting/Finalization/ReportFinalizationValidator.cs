using Rah_Negar.Core.Reporting.Projection;

namespace Rah_Negar.Foundation.Application.Reporting.Finalization;

public sealed class ReportFinalizationValidator : IReportFinalizationValidator
{
    public FinalizationValidationResult Validate(ReportFinalizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var identity = new List<FinalizationValidationIssue>();
        var evidence = new List<FinalizationValidationIssue>();
        var incomplete = new List<FinalizationValidationIssue>();
        var versions = new List<FinalizationValidationIssue>();
        var source = new List<FinalizationValidationIssue>();
        ReportProjection projection = request.Projection;

        Required(request.FinalizationId, "finalization.identity.missing", "FinalizationId", identity);
        Required(request.SnapshotId, "snapshot.identity.missing", "SnapshotId", identity);
        Required(request.ActorIdentity, "finalization.actor.missing", "ActorIdentity", identity);
        Required(request.FinalizationPolicyVersion, "version.finalization-policy.missing", "FinalizationPolicyVersion", versions);
        Required(request.SnapshotIntegrityVersion, "version.snapshot-integrity.missing", "SnapshotIntegrityVersion", versions);
        Required(request.ExpectedSourceRevision, "evidence.expected-source-revision.missing", "ExpectedSourceRevision", evidence);
        Required(request.VerifiedSourceRevision, "evidence.verified-source-revision.missing", "VerifiedSourceRevision", evidence);

        if (!StringComparer.Ordinal.Equals(request.ExpectedStationId, projection.Identity.StationId))
            identity.Add(Issue("identity.station.mismatch", "Projection Station does not match the request."));
        if (request.ExpectedPeriodStartMinute != projection.Identity.PeriodStartMinute ||
            request.ExpectedPeriodEndMinute != projection.Identity.PeriodEndMinute)
            identity.Add(Issue("identity.period.mismatch", "Projection period does not match the request."));
        if (!request.ExpectedUnitIds.SequenceEqual(projection.Identity.UnitIds, StringComparer.Ordinal))
            identity.Add(Issue("identity.units.mismatch", "Projection Units do not match the request."));
        if (projection.Identity.SourceMode != ReportSourceMode.OpenProjection)
            identity.Add(Issue("identity.source-mode.invalid", "Only an open projection can be captured."));
        if (request.ExpectedPeriodStartMinute >= request.ExpectedPeriodEndMinute)
            identity.Add(Issue("identity.period.invalid", "The requested period must be non-empty."));
        if (request.SnapshotSequence < 1 ||
            (request.SnapshotSequence == 1 && !string.IsNullOrWhiteSpace(request.SupersedesSnapshotId)) ||
            (request.SnapshotSequence > 1 && string.IsNullOrWhiteSpace(request.SupersedesSnapshotId)))
            identity.Add(Issue("identity.snapshot-lineage.invalid", "Snapshot sequence and supersession identity are inconsistent."));

        if (projection.Status != ReportProjectionStatus.Complete ||
            projection.Completeness.State != CompletenessState.Complete ||
            !projection.Completeness.IsFinalizationEligible || projection.BlockingReasons.Count != 0)
            incomplete.Add(Issue("completeness.not-eligible", "The projection is not eligible for finalization."));

        ValidateEvidence(projection.Evidence, evidence);
        foreach (string error in projection.Versions.ValidateFor(projection.Identity.UnitIds))
            versions.Add(new FinalizationValidationIssue(error, "A required projection version is missing."));

        if (!StringComparer.Ordinal.Equals(request.ExpectedSourceRevision, projection.Evidence.SourceRevision))
            source.Add(Issue("source.projection-revision.mismatch", "Projection evidence differs from the expected source revision."));
        if (!StringComparer.Ordinal.Equals(request.ExpectedSourceRevision, request.VerifiedSourceRevision))
            source.Add(Issue("source.changed", "The verified source revision changed after projection generation."));

        if (source.Count != 0) return FinalizationValidationResult.Invalid(ReportFinalizationOutcome.SourceChangedRejected, source);
        if (identity.Count != 0 || evidence.Count != 0)
            return FinalizationValidationResult.Invalid(ReportFinalizationOutcome.ValidationRejected, identity.Concat(evidence));
        if (versions.Count != 0)
            return FinalizationValidationResult.Invalid(ReportFinalizationOutcome.VersionRejected, versions);
        if (incomplete.Count != 0)
            return FinalizationValidationResult.Invalid(ReportFinalizationOutcome.IncompleteRejected, incomplete);
        return FinalizationValidationResult.Valid();
    }

    private static void ValidateEvidence(ReportEvidence value, ICollection<FinalizationValidationIssue> issues)
    {
        Required(value.SourceRevision, "evidence.source-revision.missing", "SourceRevision", issues);
        Required(value.HourlyRevision, "evidence.hourly-revision.missing", "HourlyRevision", issues);
        Required(value.DailyRevision, "evidence.daily-revision.missing", "DailyRevision", issues);
        Required(value.StationProfileIdentity, "evidence.station-profile.missing", "StationProfileIdentity", issues);
        Required(value.CalendarIdentity, "evidence.calendar.missing", "CalendarIdentity", issues);
        Required(value.OrderingConvention, "evidence.ordering.missing", "OrderingConvention", issues);
        if (value.HourlyRecordCount < 0 || value.DailyRecordCount < 0)
            issues.Add(Issue("evidence.record-count.invalid", "Evidence record counts cannot be negative."));
    }

    private static void Required(string value, string code, string field,
        ICollection<FinalizationValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)) issues.Add(new(code, $"{field} is required.", Field: field));
    }
    private static FinalizationValidationIssue Issue(string code, string message) => new(code, message);
}
