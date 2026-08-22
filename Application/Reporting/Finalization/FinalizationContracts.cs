using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Reporting.Snapshot;

namespace Rah_Negar.Foundation.Application.Reporting.Finalization;

public enum ReportFinalizationOutcome
{
    Succeeded,
    IncompleteRejected,
    VersionRejected,
    SourceChangedRejected,
    ValidationRejected
}

public sealed record FinalizationValidationIssue(string Code, string Message,
    string? UnitId = null, string? Field = null);

public sealed class FinalizationValidationResult
{
    private FinalizationValidationResult(ReportFinalizationOutcome outcome,
        IEnumerable<FinalizationValidationIssue> issues)
    {
        Outcome = outcome;
        Issues = Array.AsReadOnly(issues.OrderBy(x => x.UnitId, StringComparer.Ordinal)
            .ThenBy(x => x.Field, StringComparer.Ordinal).ThenBy(x => x.Code, StringComparer.Ordinal).ToArray());
    }

    public bool IsValid => Outcome == ReportFinalizationOutcome.Succeeded && Issues.Count == 0;
    public ReportFinalizationOutcome Outcome { get; }
    public IReadOnlyList<FinalizationValidationIssue> Issues { get; }

    public static FinalizationValidationResult Valid() =>
        new(ReportFinalizationOutcome.Succeeded, Array.Empty<FinalizationValidationIssue>());

    public static FinalizationValidationResult Invalid(ReportFinalizationOutcome outcome,
        IEnumerable<FinalizationValidationIssue> issues)
    {
        if (outcome == ReportFinalizationOutcome.Succeeded)
            throw new ArgumentException("An invalid result cannot have a successful outcome.", nameof(outcome));
        FinalizationValidationIssue[] values = issues.ToArray();
        if (values.Length == 0) throw new ArgumentException("A rejection requires an issue.", nameof(issues));
        return new(outcome, values);
    }
}

public sealed class ReportFinalizationRequest
{
    public ReportFinalizationRequest(string finalizationId, string snapshotId, ReportProjection projection,
        string expectedStationId, long expectedPeriodStartMinute, long expectedPeriodEndMinute,
        IEnumerable<string> expectedUnitIds, string expectedSourceRevision, string verifiedSourceRevision,
        int snapshotSequence, string? supersedesSnapshotId, string actorIdentity,
        DateTimeOffset finalizedAt, string finalizationPolicyVersion, string snapshotIntegrityVersion)
    {
        FinalizationId = finalizationId;
        SnapshotId = snapshotId;
        Projection = projection ?? throw new ArgumentNullException(nameof(projection));
        ExpectedStationId = expectedStationId;
        ExpectedPeriodStartMinute = expectedPeriodStartMinute;
        ExpectedPeriodEndMinute = expectedPeriodEndMinute;
        ExpectedUnitIds = Array.AsReadOnly((expectedUnitIds ?? throw new ArgumentNullException(nameof(expectedUnitIds)))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray());
        ExpectedSourceRevision = expectedSourceRevision;
        VerifiedSourceRevision = verifiedSourceRevision;
        SnapshotSequence = snapshotSequence;
        SupersedesSnapshotId = supersedesSnapshotId;
        ActorIdentity = actorIdentity;
        FinalizedAt = finalizedAt;
        FinalizationPolicyVersion = finalizationPolicyVersion;
        SnapshotIntegrityVersion = snapshotIntegrityVersion;
    }

    public string FinalizationId { get; }
    public string SnapshotId { get; }
    public ReportProjection Projection { get; }
    public string ExpectedStationId { get; }
    public long ExpectedPeriodStartMinute { get; }
    public long ExpectedPeriodEndMinute { get; }
    public IReadOnlyList<string> ExpectedUnitIds { get; }
    public string ExpectedSourceRevision { get; }
    public string VerifiedSourceRevision { get; }
    public int SnapshotSequence { get; }
    public string? SupersedesSnapshotId { get; }
    public string ActorIdentity { get; }
    public DateTimeOffset FinalizedAt { get; }
    public string FinalizationPolicyVersion { get; }
    public string SnapshotIntegrityVersion { get; }
}

public sealed class ReportFinalizationResult
{
    private ReportFinalizationResult(ReportFinalizationOutcome outcome,
        FinalizedReportSnapshot? snapshot, IReadOnlyList<FinalizationValidationIssue> issues)
    {
        Outcome = outcome;
        Snapshot = snapshot;
        Issues = issues;
    }

    public bool IsSuccess => Outcome == ReportFinalizationOutcome.Succeeded && Snapshot is not null;
    public ReportFinalizationOutcome Outcome { get; }
    public FinalizedReportSnapshot? Snapshot { get; }
    public IReadOnlyList<FinalizationValidationIssue> Issues { get; }

    public static ReportFinalizationResult Success(FinalizedReportSnapshot snapshot) =>
        new(ReportFinalizationOutcome.Succeeded,
            snapshot ?? throw new ArgumentNullException(nameof(snapshot)), Array.Empty<FinalizationValidationIssue>());

    public static ReportFinalizationResult Rejected(FinalizationValidationResult validation) =>
        validation.IsValid
            ? throw new ArgumentException("A valid result cannot create a rejection.", nameof(validation))
            : new(validation.Outcome, null, validation.Issues);
}

public interface IReportFinalizationValidator
{
    FinalizationValidationResult Validate(ReportFinalizationRequest request);
}

public interface IReportSnapshotFactory
{
    ReportFinalizationResult Create(ReportFinalizationRequest request,
        FinalizationValidationResult validation);
}
